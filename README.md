# iTeachSkillsApp

The iTeachSkills App for HoloLens2 developed by Unity

## Contents

- [iTeachSkillsApp](#iteachskillsapp)
  - [Contents](#contents)
  - [Environment Setup](#environment-setup)
      - [1. Create Conda Environment](#1-create-conda-environment)
      - [2. Install ROS1 Melodic as instructed in RoboStack](#2-install-ros1-melodic-as-instructed-in-robostack)
      - [3. Compile `ros_tcp_endpoint` Package in ROS1 Melodic](#3-compile-ros_tcp_endpoint-package-in-ros1-melodic)
  - [How to Test on Linux](#how-to-test-on-linux)
      - [Terminal 1: Start ROS1 Melodic](#terminal-1-start-ros1-melodic)
      - [Terminal 2: Launch ros\_tcp\_endpoint](#terminal-2-launch-ros_tcp_endpoint)
      - [Terminal 3: Publish Images from a Video or a Scene Directory](#terminal-3-publish-images-from-a-video-or-a-scene-directory)
      - [Terminal 4: Run RVIZ to Visualize the Images](#terminal-4-run-rviz-to-visualize-the-images)
  - [How to Test on Windows](#how-to-test-on-windows)
      - [Terminal 1: Start ROS1 Melodic](#terminal-1-start-ros1-melodic-1)
      - [Terminal 2: Launch ros\_tcp\_endpoint](#terminal-2-launch-ros_tcp_endpoint-1)
      - [Terminal 3: Publish test images](#terminal-3-publish-test-images)

## Environment Setup

#### 1. Create Conda Environment

- Create conda environment

```bash
mamba create -n iteachskills python=3.11
```

- Activate conda environment

```bash
mamba activate iteachskills
```

#### 2. Install ROS1 Melodic as instructed in [RoboStack](https://robostack.github.io/)

- Setup channels

```bash
conda config --env --add channels conda-forge
conda config --env --add channels robostack-staging
conda config --env --remove channels defaults
```

- Install ROS1 Melodic

```bash
mamba install ros-noetic-desktop
```

- Reactivate conda environment

```bash
mamba deactivate
mamba activate iteachskills
```

- Install tools for local development

```bash
mamba install compilers cmake pkg-config make ninja colcon-common-extensions catkin_tools rosdep
```

- Additional dependencies for developing on windows (optional)

```bash
# Install the Visual Studio command prompt - if you use Visual Studio 2019:
mamba install vs2019_win-64

# Install the Visual Studio command prompt - if you use Visual Studio 2022:
mamba install vs2022_win-64
```

#### 3. Compile `ros_tcp_endpoint` Package in ROS1 Melodic

- Clone the `ros_tcp_endpoint` package

```bash
cd ~/catkin_ws/src
git clone 'https://github.com/Unity-Technologies/ROS-TCP-Endpoint.git'
```

- Build the `ros_tcp_endpoint` package

```bash
cd ~/catkin_ws
catkin_make
```

## How to Test on Linux

#### Terminal 1: Start ROS1 Melodic

```bash
mamba activate iteachskills
roscore
```

#### Terminal 2: Launch ros_tcp_endpoint

```bash
mamba activate iteachskills
# Source Catwin Workspace
source ~/catkin_ws/devel/setup.bash
# Launch ros_tcp_endpoint
roslaunch ros_tcp_endpoint endpoint.launch
```

#### Terminal 3: Publish Images from a Video or a Scene Directory

1. Publish test images from the demo.mp4 video

```bash
mamba activate iteachskills
python Python/image_publisher.py
```

2. Publish images from a scene directory

```bash
mamba activate iteachskills
python Python/image_publisher.py <path_to_scene_dir>
```

- The label results will be saved under the `output/prompts` directory.
- The the prompts for SAM2 could be obtained by:
  ```python
  import json
  W = 640 # Image width
  H = 480 # Image height
  with open('prompts.json', 'r') as f:
      prompts = json.load(f)
  sam2_point_prompts = []
  for prompt in prompts:
      sam2_points = [
        (int(pt["x"] * W), int((1 - pt["y"]) * H, label))
        for pt, label in zip(prompt["points"], prompt["labels"])
        ]
      sam2_point_prompts.append(sam2_points)
  ```

#### Terminal 4: Run RVIZ to Visualize the Images

```bash
mamba activate iteachskills
rviz -d Python/image_viewer.rviz
```

## How to Test on Windows

#### Terminal 1: Start ROS1 Melodic

```bash
mamba activate iteachskills
roscore
```

#### Terminal 2: Launch ros_tcp_endpoint

```bash
mamba activate iteachskills
# Source Catwin Workspace
call C:/Users/JikaiWang/Catkin/devel/setup.bat
# Launch ros_tcp_endpoint
roslaunch ros_tcp_endpoint endpoint.launch
```

#### Terminal 3: Publish test images

```bash
mamba activate iteachskills
python Python/image_publisher.py
```
