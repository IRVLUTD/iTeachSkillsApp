# iTeachSkillsApp

The iTeachSkills App for HoloLens2 developed by Unity

# Environment Setup

## Python Environment Setup

1. Create conda environment

```bash
mamba create -n iteachskills python=3.11
```

2. Activate conda environment

```bash
mamba activate iteachskills
```

3. Install ROS1 Melodic as instructed in [RoboStack](https://robostack.github.io/)

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

## How to Test on Windows

1. Terminal 1: Start ROS1 Melodic

```bash
mamba activate iteachskills
roscore
```

2. Terminal 2: Launch ros_tcp_endpoint

```bash
mamba activate iteachskills
# Source Catwin Workspace
call C:/Users/JikaiWang/Catkin/devel/setup.bat
# Launch ros_tcp_endpoint
roslaunch ros_tcp_endpoint endpoint.launch
```

3. Terminal 3: Publish test images from Webcam

```bash
mamba activate iteachskills
python Python/image_publisher.py
```
