#!/usr/bin/env python

import rospy
import cv2
from sensor_msgs.msg import CompressedImage
from cv_bridge import CvBridge, CvBridgeError

class CameraPublisher:
    def __init__(self, topic, fps=10) -> None:
        self._topic = topic
        self._pub = rospy.Publisher(self._topic, CompressedImage, queue_size=10)
        self._cap = cv2.VideoCapture(0)
        self._bridge = CvBridge()
        self._rate = rospy.Rate(fps)

    def publish(self):
        rospy.loginfo("Start publishing images to topic: %s", self._topic)
        while not rospy.is_shutdown():
            ret, frame = self._cap.read()
            if ret:
                try:
                    compressed_image = self._bridge.cv2_to_compressed_imgmsg(frame, "jpg")
                    self._pub.publish(compressed_image)
                except CvBridgeError as e:
                    rospy.logerr(e)
            else:
                rospy.logwarn("Could not capture frame")
            self._rate.sleep()

    def release(self):
        if self._cap is not None:
            self._cap.release()
        rospy.loginfo("Camera released")

def main():
    rospy.init_node('image_publisher', anonymous=True)
    camera_publisher = CameraPublisher('/head_camera/rgb/image_raw/compressed')
    camera_publisher.publish()
    camera_publisher.release()


if __name__ == '__main__':
    main()
