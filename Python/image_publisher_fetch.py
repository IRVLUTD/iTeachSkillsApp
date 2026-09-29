import rospy
import datetime
from utils import *
from ultralytics import SAM
from std_msgs.msg import Bool, String
from fetch_listener import ImageListener
from sensor_msgs.msg import CompressedImage
from cv_bridge import CvBridge, CvBridgeError



HOLOLENS_TOPICS = {
    "videoTopic": "/hololens_stream/compressed",
    "labelFrameTopic": "/head_camera/label_frame/image_raw/compressed",
    "recordCommandTopic": "/hololens/out/record_command",
    "sendPromptsTopic": "/hololens/out/prompts",
    "summaryInfoTopic": "/hololens/out/summary_info",
}


class CameraPublisher:
    def __init__(self, video_source, fps=30, debug=False) -> None:
        if Path(video_source).suffix.lower() in [".mp4", ".avi"]:
            self._cap = cv2.VideoCapture(str(video_source))
        else:
            rospy.logerr("Invalid video source. Supported formats: mp4, avi")
            return

        # Initialize the Node
        self._debug = debug
        self._init_node("image_publisher")
        self._bridge = CvBridge()
        self._rate = rospy.Rate(fps)

        # Subscribe to the Hololens topics
        # self._img_pub = rospy.Publisher(
        #     HOLOLENS_TOPICS["videoTopic"], CompressedImage, queue_size=1
        # )

        # rospy.Subscriber(
        #     HOLOLENS_TOPICS["videoTopic"], Bool, self._video_topic_callback
        # )
        
        # Subscribe to the Hololens topics
        self._img_pub = rospy.Publisher(
            HOLOLENS_TOPICS["videoTopic"], CompressedImage, queue_size=1
        )
        self._label_pub = rospy.Publisher(
            HOLOLENS_TOPICS["labelFrameTopic"], CompressedImage, queue_size=1
        )
        self._summary_pub = rospy.Publisher(
            HOLOLENS_TOPICS["summaryInfoTopic"], String, queue_size=1
        )
        rospy.Subscriber(
            HOLOLENS_TOPICS["recordCommandTopic"], Bool, self._record_command_callback
        )
        rospy.Subscriber(
            HOLOLENS_TOPICS["sendPromptsTopic"], String, self._send_prompts_callback
        )

        self.listener = ImageListener("Fetch")

        self._image_frames = []
        self._depth_frames = []
        self._image_frames_for_sam2 = []
        self._seg_bboxes_for_sam2 = []
        self._seg_masks_for_sam2 = []
        self._label_frame = None
        self._raw_label_frame = None
        self._curr_video_frame=None
        self._is_recording = False
        self._send_label_frame = False
        self._root_dir = CURR_DIR / "data_captured"
        sam2_ckpt_dir = f"{CURR_DIR}/ckpts/sam2"

        # download_file_if_not_exists(
        #     "https://github.com/ultralytics/assets/releases/download/v8.3.0/sam2.1_l.pt",
        #     sam2_ckpt_dir
        # )
        # self.sam_img_predictor = SAM(f"{sam2_ckpt_dir}/sam2_l.pt")
        self._sam_img_predictor = SAM("sam2_l.pt")
        
        

    def _init_node(self, node_name):
        self._node = rospy.init_node(node_name, anonymous=True)
        rospy.loginfo(f"Node initialized: {node_name}")

    def run(self):
        rospy.loginfo("Start publishing video frames...")

        # save directory
        now = datetime.datetime.now()
        seq_name = "scene_{:%m%dT%H%M%S}/".format(now)
        
        self._save_dir = self._root_dir / seq_name
        make_clean_folder(self._save_dir)
        
        self._uav_dir = self._save_dir / "usr_annotation_viz"
        make_clean_folder(self._uav_dir)

        _send_label_counter = 0

        while not rospy.is_shutdown():
            
            # Read and publish video frames, or loop if video is over
            # ret, frame = self._cap.read()
            # if not ret:
            #     self._cap.set(cv2.CAP_PROP_POS_FRAMES, 0)
            #     ret, frame = self._cap.read()

            try:
                # if self._debug:
                #     if self._curr_video_frame is not None:
                #         cv2.imshow("Video", self._curr_video_frame)
                #     if cv2.waitKey(1) & 0xFF == ord("q"):
                #         break

                # self._img_pub.publish(
                #     self._bridge.cv2_to_compressed_imgmsg(frame, "jpg")
                # )
                rgb, depth, RT_camera, RT_laser, robot_velocity, RT_goal = self.listener.get_data_to_save()

                if self._is_recording:
                    rospy.loginfo(f"is_recording: {self._is_recording}")
                    self._image_frames.append(rgb)
                    self._depth_frames.append(depth)

                if self._send_label_frame:
                    self._label_pub.publish(
                        self._bridge.cv2_to_compressed_imgmsg(self._label_frame, "jpg")
                    )

                    rospy.loginfo(
                        f"Sent label frame to {HOLOLENS_TOPICS['labelFrameTopic']}"
                    )

                    _local_label_frame_copy = self._label_frame.copy()

                    write_bgr_image(
                        self._save_dir / f"bbox_annotated_img.png", _local_label_frame_copy
                    )

                    write_bgr_image(
                        self._uav_dir / f"bbox_annotated_img_{_send_label_counter}.png", _local_label_frame_copy
                    )

                    rospy.loginfo(
                        f"Saved labeled image vis to {self._uav_dir / f'bbox_annotated_img_{_send_label_counter}.png'}"
                    )

                    _send_label_counter += 1
                    self._send_label_frame = False

                self._update_summary_info()
                self._summary_pub.publish(self._summary_info)

            except CvBridgeError as e:
                rospy.logerr(e)

            self._rate.sleep()

    def stop(self):
        if self._debug:
            cv2.destroyAllWindows()

        if self._cap is not None:
            self._cap.release()
        rospy.loginfo("Video source released")

    def _video_topic_callback(self, msg):
        if msg is not None:
            self._curr_video_frame = self._bridge.compressed_imgmsg_to_cv2(msg)


    def _update_summary_info(self):
        curr_date_time = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        self._summary_info = (
            f"IsRecording: {self._is_recording}\n"
            + f"IsLabeling: {self._label_frame is not None}\n"
            + f"Time: {curr_date_time}\n"
        )

    def _record_command_callback(self, msg):
        rospy.loginfo(f"Received record command: {msg.data}")
        self._is_recording = msg.data
        if not self._is_recording:
            self._save_recorded_frames()
        else:
            self._label_frame = None

    def _send_prompts_callback(self, msg):
        rospy.loginfo(f"Received prompt: {msg.data}")
        # if msg.data.lower() == "stop label":
        #     run_sam2_on_images(())
        # else:
        #     pass
        # import pdb; pdb.set_trace()
        # msg.data
        _msg=json.loads(msg.data)
        # self._label_frame = draw_prompts_on_image(self._label_frame, prompts)
        self._label_frame, self._seg_bboxes_for_sam2, self._seg_masks_for_sam2 = \
            self._draw_sam2_results(self._raw_label_frame, _msg["prompts"])
        _msg['bboxes_xyxy'] = np.array(self._seg_bboxes_for_sam2).tolist()
        save_data_to_json(self._save_dir / "prompts.json", _msg)
        self._send_label_frame = True


    def _draw_sam2_results(self, image, prompts):
        img = image.copy()
        H, W = img.shape[:2]
        seg_boxes = []
        seg_masks = []
        for prompt in prompts:
            if prompt["points"] and prompt["labels"]:
                points = [
                    (int(pt["x"] * W), int((1 - pt["y"]) * H)) for pt in prompt["points"]
                ]
                labels = prompt["labels"]
                results = self._sam_img_predictor(img, points=points, labels=labels)
                boxes = results[0].boxes
                masks = results[0].masks
                box = boxes.cpu().numpy().xyxy[0].astype(int)
                mask = masks.cpu().numpy().data[0].astype(bool)
                seg_boxes.append(box)
                seg_masks.append(mask)
        if seg_boxes and seg_masks:
            vis = annotate(img, seg_boxes, seg_masks)
        else:
            vis = img
        return vis, seg_boxes, seg_masks


    def _save_recorded_frames(self):
        if len(self._image_frames) == 0:
            rospy.logwarn("No frames to save.")
            return
        self._raw_label_frame = self._image_frames[-1].copy()
        self._label_frame = self._image_frames[-1].copy()
        self._send_label_frame = True
        save_bgr_frames(self._save_dir, self._image_frames)
        save_depth_frames(self._save_dir, self._depth_frames)
        rospy.loginfo(f"Saved {len(self._image_frames)} frames to {self._save_dir}")
        
        self._image_frames.reverse() # last frame should be the first
        self._image_frames_for_sam2 = self._image_frames
        self._image_frames = []
        self._depth_frames = []


def main():
    camera_publisher = CameraPublisher(video_source=CURR_DIR / "demo.mp4", debug=False)
    camera_publisher.run()
    camera_publisher.stop()


if __name__ == "__main__":
    main()
