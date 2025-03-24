import rospy
from std_msgs.msg import Bool, String
from sensor_msgs.msg import CompressedImage
from cv_bridge import CvBridge, CvBridgeError
from utils import *


HOLOLENS_TOPICS = {
    "videoTopic": "/head_camera/rgb/image_raw/compressed",
    "labelFrameTopic": "/head_camera/label_frame/image_raw/compressed",
    "recordCommandTopic": "/hololens/out/record_command",
    "sendPromptsTopic": "/hololens/out/prompts",
}


class CameraPublisher:
    def __init__(self, images_source, fps=30, debug=False) -> None:
        # Initialize the Node
        self._debug = debug
        self._init_node("image_publisher")
        self._bridge = CvBridge()
        self._rate = rospy.Rate(fps)

        # Load images from the source
        self._image_frames = self._read_rgb_images(images_source)
        self._num_frames = len(self._image_frames)

        # Subscribe to the Hololens topics
        self._img_pub = rospy.Publisher(
            HOLOLENS_TOPICS["videoTopic"], CompressedImage, queue_size=1
        )
        self._label_pub = rospy.Publisher(
            HOLOLENS_TOPICS["labelFrameTopic"], CompressedImage, queue_size=1
        )
        rospy.Subscriber(
            HOLOLENS_TOPICS["recordCommandTopic"], Bool, self._record_command_callback
        )
        rospy.Subscriber(
            HOLOLENS_TOPICS["sendPromptsTopic"], String, self._send_prompts_callback
        )

        self._label_frame = None
        self._is_recording = False
        self._send_label_frame = False
        self._save_dir = CURR_DIR / "output" / "prompts" / Path(images_source).name
        make_clean_folder(self._save_dir)
        self._frame_id = 0

    def _read_rgb_images(self, images_source):
        image_files = sorted(Path(images_source).glob("rgb/*.png"))
        num_frames = len(image_files)
        tqbar = tqdm(total=num_frames, desc="Loading images")
        image_frames = [None] * num_frames
        with concurrent.futures.ThreadPoolExecutor() as executor:
            futures = {
                executor.submit(read_bgr_image, str(image_file)): i
                for i, image_file in enumerate(image_files)
            }
            for future in concurrent.futures.as_completed(futures):
                i = futures[future]
                image_frames[i] = future.result()
                tqbar.update(1)
            futures.clear()
        tqbar.close()
        return image_frames

    def _init_node(self, node_name):
        self._node = rospy.init_node(node_name, anonymous=True)
        rospy.loginfo(f"Node initialized: {node_name}")

    def run(self):
        rospy.loginfo("Start publishing video frames...")

        if self._debug:
            # Show the video frames in a window
            cv2.namedWindow("Video", cv2.WINDOW_NORMAL)
            cv2.resizeWindow("Video", 640, 480)

        while not rospy.is_shutdown():
            # Read and publish video frames, or loop if video is over
            frame = self._image_frames[self._frame_id]
            self._frame_id = (self._frame_id + 1) % self._num_frames

            try:
                if self._debug:
                    cv2.imshow("Video", frame)
                    if cv2.waitKey(1) & 0xFF == ord("q"):
                        break

                self._img_pub.publish(
                    self._bridge.cv2_to_compressed_imgmsg(frame, "jpg")
                )
                if self._is_recording:
                    rospy.loginfo(f"is_recording: {self._is_recording}")

                if self._send_label_frame:
                    self._label_pub.publish(
                        self._bridge.cv2_to_compressed_imgmsg(self._label_frame, "jpg")
                    )
                    rospy.loginfo(
                        f"Sent label frame to {HOLOLENS_TOPICS['labelFrameTopic']}"
                    )
                    write_bgr_image(
                        self._save_dir / "vis_label_image.jpg", self._label_frame
                    )
                    rospy.loginfo(
                        f"Saved labeled image vis to {CURR_DIR / 'vis_label_image.jpg'}"
                    )
                    self._send_label_frame = False
            except CvBridgeError as e:
                rospy.logerr(e)

            self._rate.sleep()

    def stop(self):
        if self._debug:
            cv2.destroyAllWindows()
        rospy.loginfo("Video source released")

    def _record_command_callback(self, msg):
        rospy.loginfo(f"Received record command: {msg.data}")
        self._is_recording = msg.data
        if not self._is_recording:
            self._save_recorded_frames()
        else:
            self._label_frame = None

    def _send_prompts_callback(self, msg):
        rospy.loginfo(f"Received prompt: {msg.data}")
        save_data_to_json(self._save_dir / "prompts.json", msg.data)
        prompts = json.loads(msg.data)["prompts"]
        self._label_frame = draw_prompts_on_image(self._label_frame, prompts)
        self._send_label_frame = True

    def _save_recorded_frames(self):
        self._label_frame = self._image_frames[-1]
        self._send_label_frame = True


def main():
    args_parser = argparse.ArgumentParser()
    args_parser.add_argument(
        "scene_folder",
        type=str,
        required=True,
        help="Path to the folder containing the scene folder",
    )
    args = args_parser.parse_args()

    camera_publisher = CameraPublisher(images_source=args.scene_folder, debug=False)
    camera_publisher.run()
    camera_publisher.stop()


if __name__ == "__main__":
    main()
