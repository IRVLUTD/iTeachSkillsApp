<div align="center">

# 🥽 iTeachSkillsApp

### Hands-free, gaze + voice labelling on HoloLens 2 for [iTeach](https://irvlutd.github.io/iTeach/)

<br>

[![Project Page](https://img.shields.io/badge/Project-Page-2ea44f?style=for-the-badge)](https://irvlutd.github.io/iTeach/)
[![arXiv](https://img.shields.io/badge/arXiv-2410.09072-b31b1b?style=for-the-badge)](https://arxiv.org/abs/2410.09072)

![Unity](https://img.shields.io/badge/Unity-2022.3.60f1-000000?logo=unity&logoColor=white)
![HoloLens 2](https://img.shields.io/badge/HoloLens-2-0078D4?logo=microsoft&logoColor=white)
![ROS](https://img.shields.io/badge/ROS-Noetic-22314E?logo=ros&logoColor=white)
![Python](https://img.shields.io/badge/Python-3.11-3776AB?logo=python&logoColor=white)
![SAM2](https://img.shields.io/badge/SAM2-ultralytics-8A2BE2)

<br>

[**System Overview**](#-system-overview) &nbsp;·&nbsp;
[**Run on the Robot**](#-running-the-live-system-on-the-robot) &nbsp;·&nbsp;
[**Build the App**](#️-build-and-deploy-the-hololens-2-app) &nbsp;·&nbsp;
[**Configure ROS**](#-point-the-app-at-your-ros-server) &nbsp;·&nbsp;
[**Environment**](#-environment-setup) &nbsp;·&nbsp;
[**Test Offline**](#-test-without-the-robot) &nbsp;·&nbsp;
[**Output Format**](#-output-format-and-hand-off-to-iteach-uois)

</div>

<br>

The HoloLens 2 app and ROS bridge used in the current version of **iTeach**. While the robot works, you see its perception model's predictions in mixed reality. When the model fails, you record a short **HumanPlay** clip, then label the last frame **hands-free** with your eyes and voice. SAM2 turns those points into boxes and masks, which become training data for the next round.

<br>

## 🧩 Part of the iTeach Family

iTeach is split into three repositories, one per module:

<table>
  <tr>
    <th width="33%"><a href="https://github.com/IRVLUTD/iTeach">📍 iTeach</a></th>
    <th width="33%">🥽 iTeachSkillsApp <sub>(this repo)</sub></th>
    <th width="33%"><a href="https://github.com/IRVLUTD/iTeach-UOIS">🧠 iTeach-UOIS</a></th>
  </tr>
  <tr>
    <td>Project hub: overview, links, HoloLens and robot networking utilities</td>
    <td>HoloLens 2 app + ROS bridge: record HumanPlay, gaze + voice point prompts, SAM2 preview</td>
    <td>SAM2 mask propagation, MSMFormer fine-tuning and evaluation</td>
  </tr>
</table>

<br>

## 📑 Contents

<table>
  <tr><th width="4%">#</th><th width="34%">Section</th><th>Go here to…</th></tr>
  <tr><td align="center">1</td><td><a href="#-system-overview"><b>🧭 System Overview</b></a></td><td>See how the robot, laptop and HoloLens fit together<br><small>↳ <a href="#-the-teaching-loop">Teaching loop</a> · <a href="#️-architecture">Architecture</a> · <a href="#-one-iteach-round">One iTeach round</a></small></td></tr>
  <tr><td align="center">2</td><td><a href="#-running-the-live-system-on-the-robot"><b>🚀 Running the Live System on the Robot</b></a></td><td>Run a full iTeach session on the Fetch: env vars + the five terminals</td></tr>
  <tr><td align="center">3</td><td><a href="#️-build-and-deploy-the-hololens-2-app"><b>🛠️ Build and Deploy the HoloLens 2 App</b></a></td><td>Build the Unity app and install it on the HoloLens<br><small>↳ <a href="#requirements">Requirements</a> · <a href="#which-unity-project">Which project</a> · <a href="#steps">Build steps</a></small></td></tr>
  <tr><td align="center">4</td><td><a href="#-point-the-app-at-your-ros-server"><b>🔌 Point the App at Your ROS Server</b></a></td><td>Change the ROS IP or video topic, without rebuilding<br><small>↳ <a href="#upload-it">Upload the config</a></small></td></tr>
  <tr><td align="center">5</td><td><a href="#-environment-setup"><b>📦 Environment Setup</b></a></td><td>Install Python, ROS Noetic and the TCP endpoint<br><small>↳ <a href="#1--conda-environment">Conda</a> · <a href="#2--ros-1-noetic-via-robostack">ROS Noetic</a> · <a href="#3--build-ros_tcp_endpoint">ros_tcp_endpoint</a></small></td></tr>
  <tr><td align="center">6</td><td><a href="#-test-without-the-robot"><b>🧪 Test Without the Robot</b></a></td><td>Try the app with a demo video or a recorded scene</td></tr>
  <tr><td align="center">7</td><td><a href="#-output-format-and-hand-off-to-iteach-uois"><b>📤 Output Format and Hand-off to iTeach-UOIS</b></a></td><td>Understand <code>prompts.json</code> and hand the data to iTeach-UOIS<br><small>↳ <a href="#promptsjson">prompts.json</a> · <a href="#scene-layout-expected-by-iteach-uois">Scene layout</a></small></td></tr>
  <tr><td align="center">·</td><td colspan="2"><a href="#-citation">📚 Citation</a> · <a href="#-contact">📬 Contact</a> · <a href="#-acknowledgements">🙏 Acknowledgements</a></td></tr>
</table>

<br>

---

<br>

## 🧭 System Overview

iTeach is a loop: the robot runs a perception model, a human catches its mistakes in mixed reality, and every correction becomes training data for the next model.

<br>

### 🔁 The teaching loop

```mermaid
flowchart LR
    A["🤖 Deploy<br/>MSMFormer"] --> B["❌ Failure<br/>spotted in MR"]
    B --> C["🤲 HumanPlay<br/>+ gaze-voice labels"]
    C --> D["🎭 SAM2 propagates<br/>masks through the clip"]
    D --> E["🏋️ Fine-tune<br/>MSMFormer"]
    E -- "f0 → f1 → f2 …" --> A

    classDef s fill:#f1f5f9,stroke:#475569,color:#0f172a
    classDef hot fill:#fee2e2,stroke:#dc2626,color:#450a0a
    classDef win fill:#dcfce7,stroke:#16a34a,color:#052e16
    class A,C,D s
    class B hot
    class E win
```

<br>

### 🏗️ Architecture

Three machines work together. Colours show where each piece runs: 🟦 robot · 🟩 laptop · 🟪 HoloLens.

```mermaid
flowchart LR
    CAM["📷 <b>Head RGB-D camera</b><br/><sub>🤖 robot</sub>"]
    MSM["<b>① MSMFormer node</b><br/>segments every frame<br/><sub>💻 laptop</sub>"]
    REC["<b>③ image_publisher_fetch.py</b><br/>records clips · SAM2 points → boxes<br/><sub>💻 laptop</sub>"]
    CMP["<b>② sub_compress_pub.py</b><br/>predictions → JPEG<br/><sub>💻 laptop</sub>"]
    RVIZ["<b>④ RViz</b><br/><sub>💻 laptop</sub>"]
    EP["🔌 <b>ros_tcp_endpoint</b> :10000<br/>+ roscore :11311<br/><sub>🤖 robot</sub>"]
    APP["🥽 <b>iTechDemo app</b><br/>live overlay · gaze + voice labels<br/><sub>HoloLens 2</sub>"]

    CAM -- "RGB-D" --> MSM
    CAM -- "RGB-D" --> REC
    MSM -- "/seg_image_refined" --> CMP
    MSM -.-> RVIZ
    CMP -- "/hololens_stream/compressed" --> EP
    REC <-- "label_frame · summary_info<br/>record_command · prompts" --> EP
    EP <== "TCP · Wi-Fi" ==> APP

    classDef robot fill:#dbeafe,stroke:#2563eb,stroke-width:2px,color:#0b1f33
    classDef laptop fill:#dcfce7,stroke:#16a34a,stroke-width:2px,color:#0b1f33
    classDef hl fill:#f3e8ff,stroke:#9333ea,stroke-width:2px,color:#0b1f33
    class CAM,EP robot
    class MSM,CMP,REC,RVIZ laptop
    class APP hl
```

<br>

| Machine | Role | Runs |
|:--|:--|:--|
| 🤖 **Robot** | ROS **server** | `roscore` and `ros_tcp_endpoint`. The HoloLens connects here. |
| 💻 **Laptop** | ROS **client** (`ROS_MASTER_URI=http://<robot-ip>:11311`) | The GPU-heavy nodes: MSMFormer ①, prediction compressor ②, recorder + SAM2 ③, RViz ④ |
| 🥽 **HoloLens 2** | Viewer + labeller | iTechDemo app, connected to the robot's endpoint over TCP |

<br>

### 🎬 One iTeach round

```mermaid
sequenceDiagram
    autonumber
    actor U as 🧑 User
    participant H as 🥽 HoloLens
    participant R as 🤖 Robot
    participant M as ① MSMFormer
    participant P as ③ Recorder + SAM2

    rect rgba(37, 99, 235, 0.08)
    Note over R,M: 👀 Watch
    R->>M: RGB-D
    M-->>H: live segmentation overlay (via ② + endpoint)
    end

    rect rgba(220, 38, 38, 0.08)
    Note over U,P: 🎬 Capture: the model got it wrong
    U->>H: 🗣️ "Begin Capture"
    H->>P: record_command = true
    Note over U,R: 🤲 HumanPlay: rearrange the objects (5–10 s)
    R->>P: RGB-D frames
    U->>H: 🗣️ "Stop Capture"
    H->>P: record_command = false
    P-->>H: last frame (label_frame)
    end

    rect rgba(147, 51, 234, 0.08)
    Note over U,P: 🎯 Label: hands-free
    loop every object
        U->>H: 👁️ gaze + 🗣️ "True Label" / "False Label"
        U->>H: 🗣️ "Next Object"
    end
    U->>H: 🗣️ "Send Label"
    H->>P: prompts (gaze points)
    Note over P: 🎭 SAM2: points → boxes + masks
    P-->>H: preview
    Note over P: 💾 save rgb/ · depth/ · prompts.json
    end
```

<sub>Afterwards, [iTeach-UOIS](https://github.com/IRVLUTD/iTeach-UOIS) propagates the masks through the clip, fine-tunes MSMFormer, and the new checkpoint is loaded back into ①.</sub>

<br>

<details>
<summary><b>📡 ROS topics</b></summary>
<br>

| Topic | From → To | Type |
|:--|:--|:--|
| `/head_camera/rgb/image_raw`<br>`/head_camera/depth_registered/image_raw` | robot → MSMFormer node, recorder | `sensor_msgs/Image` |
| `/seg_image_refined` (also `/seg_image`, `/seg_label`, …) | MSMFormer node → `sub_compress_pub.py`, RViz | `sensor_msgs/Image` |
| `/hololens_stream/compressed` | `sub_compress_pub.py` → HoloLens (`VideoTopic`) | `sensor_msgs/CompressedImage` |
| `/head_camera/label_frame/image_raw/compressed` | recorder → HoloLens | `sensor_msgs/CompressedImage` |
| `/hololens/out/summary_info` | recorder → HoloLens | `std_msgs/String` |
| `/hololens/out/record_command` | HoloLens → recorder | `std_msgs/Bool` |
| `/hololens/out/prompts` | HoloLens → recorder | `std_msgs/String` (JSON) |

</details>

<details>
<summary><b>🎙️ Voice commands</b></summary>
<br>

| Command | What it does |
|:--|:--|
| `Stream` | Open the video panel and subscribe to `VideoTopic` |
| `Begin Capture` · `Stop Capture` | Start and stop recording the HumanPlay clip |
| `True Label` · `False Label` | Add a positive / negative point where you are looking |
| `Next Object` | Start prompts for the next object |
| `Erase Label` | Remove the last object's prompts |
| `Send Label` | Publish the prompts to the laptop (SAM2 preview comes back) |
| `Stop Label` | Send the prompts one last time, clear them and close the labelling view |
| `Summary` | Toggle the status panel (`summary_info`: recording / labelling / time) |

</details>

<br>

<div align="right"><sub><a href="#-contents">⬆ back to contents</a></sub></div>

---

<br>

## 🚀 Running the Live System on the Robot

You need five terminals: four on the laptop and one on the robot.

> [!IMPORTANT]
> Every **laptop** terminal must first point at the robot's ROS master:
> ```bash
> export ROS_MASTER_URI=http://192.168.1.3:11311   # robot IP
> export ROS_HOSTNAME=192.168.1.4                  # this laptop's IP on the robot network
> ```

<br>

| # | Where | Env | Directory | Command |
|:-:|:--|:--|:--|:--|
| **0** | 🤖 robot | robot ROS | `~/catkin_ws` | `roscore` + `roslaunch ros_tcp_endpoint endpoint.launch tcp_ip:=192.168.1.3 tcp_port:=10000`<br><sub>wrapped as `setup_iTeach` on our Fetch</sub> |
| **1** | 💻 laptop | `msm38` | `iTeach-UOIS/uois-models/UnseenObjectsWithMeanShift` | `./experiments/scripts/ros_seg_transformer_test_segmentation_fetch.sh 0 <task_name> [--save]` |
| **2** | 💻 laptop | `iteachskills` | `iTeachSkillsApp/Python` | `python sub_compress_pub.py` |
| **3** | 💻 laptop | `iteachskills` | `iTeachSkillsApp` | `python Python/image_publisher_fetch.py` |
| **4** | 💻 laptop | `iteachskills` | `iTeachSkillsApp/Python` | `rviz -d image_viewer.rviz` |

<br>

Then [upload the config](#-point-the-app-at-your-ros-server) and start the **iTechDemo** app on the HoloLens.

<br>

**Where the data goes.** Each capture is saved to `Python/data_captured/scene_<MMDD>T<HHMMSS>/`:

```
scene_0826T133015/
├── rgb/                  # 640×480 frames, 000000.png …
├── depth/                # uint16 depth in millimetres, same names
├── prompts.json          # gaze-voice points + SAM2 bboxes_xyxy
└── usr_annotation_viz/   # every SAM2 preview sent back to the HoloLens
```

This folder is the input to [iTeach-UOIS](https://github.com/IRVLUTD/iTeach-UOIS#-generating-ground-truth-masks-for-new-humanplay-scenes).

<br>

> [!TIP]
> **Load the model from a previous round.** In terminal 1, switch the active block in `ros_seg_transformer_test_segmentation_fetch.sh` from `f0` (pretrained) to `f1` / `f2` (`new_ckpts/f*/model_final.pth`).

> [!NOTE]
> `image_publisher.py` (video file) and `image_publisher_label_only.py` (recorded scene folder) are **offline stand-ins** for terminal 3. See [Test without the robot](#-test-without-the-robot).

<br>

<div align="right"><sub><a href="#-contents">⬆ back to contents</a></sub></div>

---

<br>

## 🛠️ Build and Deploy the HoloLens 2 App

### Requirements

> [!NOTE]
> Building is **Windows only**. It has not been tested on other platforms.

- **Unity 2022.3.60f1**: install this exact version through Unity Hub, with the *Universal Windows Platform Build Support* module. Other 2022.3 patch releases may re-import packages differently.
- **Visual Studio 2022** with the *Universal Windows Platform development* and *Game development with C++* workloads.
- **HoloLens 2** in Developer Mode, with the Windows Device Portal enabled.

<br>

### Which Unity project?

| Project | Product name | Unity | Status |
|:--|:--|:--|:--|
| `Unity/iTechDemo` | iTechDemo | 2022.3.60f1 | ✅ **Current app, build this one.** Gaze + voice commands, SAM2 preview, `summary_info` (release 1.0.8) |
| `Unity/iTeachSkills` | iTeachSkillsAppTest | 2022.3.59f1 | 🗄️ Earlier test project, kept for reference |

MRTK 2.8.3 and the Mixed Reality OpenXR plugin are included as `.tgz` files in `Unity/iTechDemo/Packages/MixedReality/`. [ROS-TCP-Connector](https://github.com/Unity-Technologies/ROS-TCP-Connector) is downloaded from GitHub when the project is first opened, so you need internet access for that.

> [!WARNING]
> `Unity/iTechDemo/Packages/manifest.json` pulls ROS-TCP-Connector from its default branch **without a version pin**. If a newer upstream release breaks the build, pin it by appending `#v0.7.1` to that URL (the version pinned in `Unity/iTeachSkills`).

<br>

### Steps

1. Open **`Unity/iTechDemo`** in Unity Hub.

2. Go to *File → Build Settings*: select **Universal Windows Platform**, set Architecture to **ARM64**, and click **Switch Platform**.

3. Click **Build** and choose an empty folder.
   <sub>The ROS IP is read at runtime from `ROSConnectionConfig.json` ([next section](#-point-the-app-at-your-ros-server)), so you don't need to rebuild when it changes.</sub>

4. Open the generated `.sln` in Visual Studio 2022 and set *Release / ARM64*. Then run *Project → Publish → Create App Packages → Sideloading* to produce an `.msix` / `.appx`.

5. Install the package through the Windows Device Portal (*Views → Apps → Deploy apps*).

<br>

📺 A video walkthrough of the same Unity → Visual Studio → Device Portal flow (for the earlier iTeach app) is [here](https://www.youtube.com/watch?v=kvzMAMyluJU).

<br>

<div align="right"><sub><a href="#-contents">⬆ back to contents</a></sub></div>

---

<br>

## 🔌 Point the App at Your ROS Server

The app looks for `ROSConnectionConfig.json` in two places, in this order:

| Order | Location | Notes |
|:-:|:--|:--|
| 1️⃣ | **`LocalAppData/<package>/LocalState/`** on the HoloLens | `Application.persistentDataPath`. **Put your config here.** |
| 2️⃣ | `Unity/iTechDemo/Assets/StreamingAssets/ROSConnectionConfig.json` | Built into the app. Its `RosIPAddress` is a lab IP. |

> [!TIP]
> Because of this lookup order, **you don't need to rebuild the app to change the ROS IP**. Upload a new config to LocalState instead.

<br>

This is the config used with the Fetch:

```json
{
  "RosIPAddress": "192.168.1.3",
  "RosPort": 10000,
  "KeepaliveTime": 1,
  "NetworkTimeoutSeconds": 3,
  "SleepTimeSeconds": 0.01,
  "ShowHud": false,
  "VideoTopic": "/hololens_stream/compressed",
  "LabelFrameTopic": "/head_camera/label_frame/image_raw/compressed",
  "RecordCommandTopic": "/hololens/out/record_command",
  "SendPromptsTopic": "/hololens/out/prompts",
  "SummaryInfoTopic": "/hololens/out/summary_info",
  "ImageHeight": 480,
  "ImageWidth": 640
}
```

- **`RosIPAddress`** is the IP of the machine running `ros_tcp_endpoint` (the robot, in the live setup), as seen from the HoloLens.
- **`VideoTopic`** selects what the HoloLens shows:
  - `/hololens_stream/compressed` for MSMFormer predictions
  - `/head_camera/rgb/image_raw/compressed` for the raw robot view (used by the offline `image_publisher*.py` scripts)

> [!IMPORTANT]
> The file must contain **all** of these keys. Missing topics are read as empty.

<br>

### Upload it

**Option A: Windows Device Portal**
*System → File explorer → LocalAppData → iTechDemo_… → LocalState* → upload

**Option B: script** (from [IRVLUTD/iTeach](https://github.com/IRVLUTD/iTeach) `src/`, with `HOLO_DEVICE_IP`, `HOLO_DEVICE_USERNAME`, `HOLO_DEVICE_PASSWORD` set)

```bash
python hololens_utils/HoloDevicePortal.py --app_name iTechDemo --file_path ROSConnectionConfig.json
```

Restart the app after uploading. 🔄

<br>

<div align="right"><sub><a href="#-contents">⬆ back to contents</a></sub></div>

---

<br>

## 📦 Environment Setup

### 1 · Conda environment

```bash
mamba create -n iteachskills python=3.11
mamba activate iteachskills

# PyTorch 2.5.1 + CUDA 11.8
python -m pip install torch==2.5.1 torchvision==0.20.1 --index-url https://download.pytorch.org/whl/cu118 --no-cache-dir

# Python dependencies used in Python/
python -m pip install ultralytics supervision tqdm opencv-python pillow requests --no-cache-dir
```

<sub>The SAM2 weights are downloaded automatically by ultralytics the first time they are used.</sub>

<br>

### 2 · ROS 1 Noetic via [RoboStack](https://robostack.github.io/)

```bash
# Channels
conda config --env --add channels conda-forge
conda config --env --add channels robostack-staging
conda config --env --remove channels defaults

# ROS Noetic (includes rospy, cv_bridge, tf, message_filters) + ros_numpy
mamba install ros-noetic-desktop ros-noetic-ros-numpy

# Reactivate
mamba deactivate && mamba activate iteachskills

# Build tools
mamba install compilers cmake pkg-config make ninja colcon-common-extensions catkin_tools rosdep
```

<details>
<summary>🪟 Extra dependencies for developing on Windows (optional)</summary>
<br>

```bash
# Visual Studio 2019 command prompt
mamba install vs2019_win-64

# Visual Studio 2022 command prompt
mamba install vs2022_win-64
```

</details>

<br>

### 3 · Build `ros_tcp_endpoint`

```bash
cd ~/catkin_ws/src
git clone https://github.com/Unity-Technologies/ROS-TCP-Endpoint.git

cd ~/catkin_ws
catkin_make
```

<br>

<div align="right"><sub><a href="#-contents">⬆ back to contents</a></sub></div>

---

<br>

## 🧪 Test Without the Robot

These steps run the whole app loop on a single machine, fed by a video or a recorded scene instead of the Fetch.

<br>

### 🐧 Linux

**Terminal 1: ROS master**

```bash
mamba activate iteachskills
roscore
```

**Terminal 2: TCP endpoint** (the `tcp_ip` must be reachable from the HoloLens)

```bash
mamba activate iteachskills
source ~/catkin_ws/devel/setup.bash
roslaunch ros_tcp_endpoint endpoint.launch tcp_ip:=<this-machine-ip> tcp_port:=10000
```

**Terminal 3: publish images.** Pick one:

| Mode | Command | Output |
|:--|:--|:--|
| 🎞️ **Demo video** (app / UI smoke test) | `python Python/image_publisher.py` | `Python/recordings/` |
| 🗂️ **Recorded scene** (the labelling mode used for the dataset) | `python Python/image_publisher_label_only.py --scene_folder <scene_dir>` | `Python/output/prompts/<scene_name>/prompts.json` |

<sub>`<scene_dir>` must contain an `rgb/` folder of PNG frames. When you stop recording in the app, the **last** frame is sent to the HoloLens for labelling.</sub>

**Terminal 4: RViz**

```bash
mamba activate iteachskills
rviz -d Python/image_viewer.rviz
```

> [!NOTE]
> For these offline scripts, set `VideoTopic` to `/head_camera/rgb/image_raw/compressed` in the config.

<br>

<details>
<summary><b>🪟 Windows</b></summary>
<br>

**Terminal 1: ROS master**

```bat
mamba activate iteachskills
roscore
```

**Terminal 2: TCP endpoint** (adjust the path to where you built `ros_tcp_endpoint`)

```bat
mamba activate iteachskills
call %USERPROFILE%/catkin_ws/devel/setup.bat
roslaunch ros_tcp_endpoint endpoint.launch
```

**Terminal 3: publish test images**

```bat
mamba activate iteachskills
python Python/image_publisher.py
```

</details>

<br>

<div align="right"><sub><a href="#-contents">⬆ back to contents</a></sub></div>

---

<br>

## 📤 Output Format and Hand-off to iTeach-UOIS

### `prompts.json`

```json
{
  "prompts": [
    {"points": [{"x": 0.41, "y": 0.62}], "labels": [1]}
  ],
  "bboxes_xyxy": [[265, 208, 317, 234]]
}
```

| Key | Meaning |
|:--|:--|
| `prompts` | One entry per object, as sent by the HoloLens. `x`, `y` are normalized to [0, 1], and **`y` is measured from the bottom** of the image. `labels` are SAM point labels (1 = foreground, 0 = background). |
| `bboxes_xyxy` | Pixel boxes `[x1, y1, x2, y2]` from running SAM2 on those points on the last frame. **This is what iTeach-UOIS reads.** |

<br>

> [!TIP]
> Older captures without `bboxes_xyxy`? Add them offline:
> ```bash
> python Python/sam2_test.py --scene_folder <scene_dir>   # writes bboxes_xyxy back into the prompt file
> ```

<details>
<summary>🐍 Convert the points to SAM2 pixel coordinates yourself</summary>
<br>

```python
import json

W, H = 640, 480  # frame size

with open("prompts.json") as f:
    prompts = json.load(f)["prompts"]

sam2_point_prompts = []
for prompt in prompts:
    sam2_points = [
        (int(pt["x"] * W), int((1 - pt["y"]) * H), label)
        for pt, label in zip(prompt["points"], prompt["labels"])
    ]
    sam2_point_prompts.append(sam2_points)
```

</details>

<br>

### Scene layout expected by iTeach-UOIS

```
scene_XXX/
├── rgb/000000.png, 000001.png, …   # 640×480 RGB, zero-padded sequential names (time order)
├── depth/000000.png, …             # 16-bit depth in millimetres, same names as rgb/
└── prompts.json                    # from data_captured/ (live) or output/prompts/<scene>/ (offline)
```

➡️ Next: [**Generating ground-truth masks**](https://github.com/IRVLUTD/iTeach-UOIS#-generating-ground-truth-masks-for-new-humanplay-scenes) in iTeach-UOIS.

<br>

<div align="right"><sub><a href="#-contents">⬆ back to contents</a></sub></div>

---

<br>

## 📚 Citation

If ***iTeach*** helps your research, please cite:

```bibtex
@misc{padalunkal2024iteach,
  title         = {iTeach: In the Wild Interactive Teaching for Failure-Driven Adaptation of Robot Perception},
  author        = {Jishnu Jaykumar P and Cole Salvato and Vinaya Bomnale and Jikai Wang and Ayush Bhardwaj and Jin-Ryong Kim and Yu Xiang},
  year          = {2026},
  eprint        = {2410.09072},
  archivePrefix = {arXiv},
  primaryClass  = {cs.RO},
  url           = {https://arxiv.org/abs/2410.09072}
}
```

<br>

## 📬 Contact

| | |
|:--|:--|
| 💬 Questions & ideas | [Discussion forum](https://github.com/IRVLUTD/iTeach/discussions) |
| 🛠️ Bugs | [Open an issue](https://github.com/IRVLUTD/iTeach/issues) |
| 📧 Direct | [Jishnu](https://jishnujayakumar.github.io/) |

<br>

## 🙏 Acknowledgements

This work was supported by the DARPA Perceptually-enabled Task Guidance (PTG) Program under contract number HR00112220005, the Sony Research Award Program, and the National Science Foundation (NSF) under Grant No. 2346528. We thank [Sai Haneesh Allu](https://saihaneeshallu.github.io/) for assistance with the real-world experiments.

<br>

<div align="center">
<sub>Built at the <a href="https://labs.utdallas.edu/irvl/">Intelligent Robotics and Vision Lab</a>, The University of Texas at Dallas</sub>
</div>
