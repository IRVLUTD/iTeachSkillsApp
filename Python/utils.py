import shutil
import json
import cv2
import numpy as np
from pathlib import Path
import concurrent.futures


CURR_DIR = Path(__file__).resolve().parent


def make_clean_folder(folder):
    dir = Path(folder)
    if Path(folder).exists():
        shutil.rmtree(dir)
    dir.mkdir(exist_ok=True)


def write_rgb_image(file_path, image):
    cv2.imwrite(str(file_path), cv2.cvtColor(image, cv2.COLOR_RGB2BGR))


def write_bgr_image(file_path, image):
    cv2.imwrite(str(file_path), image)


def read_rgb_image(file_path):
    return cv2.cvtColor(cv2.imread(str(file_path)), cv2.COLOR_BGR2RGB)


def read_bgr_image(file_path):
    return cv2.imread(str(file_path))


def save_rgb_frames(save_dir, frames):
    make_clean_folder(save_dir)
    with concurrent.futures.ThreadPoolExecutor() as executor:
        futures = {
            executor.submit(write_rgb_image, save_dir / f"color_{i:06d}.jpg", frame): i
            for i, frame in enumerate(frames)
        }
        for future in concurrent.futures.as_completed(futures):
            i = futures[future]
            future.result()


def save_bgr_frames(save_dir, frames):
    make_clean_folder(save_dir)
    with concurrent.futures.ThreadPoolExecutor() as executor:
        futures = {
            executor.submit(write_bgr_image, save_dir / f"color_{i:06d}.jpg", frame): i
            for i, frame in enumerate(frames)
        }
        for future in concurrent.futures.as_completed(futures):
            i = futures[future]
            future.result()


def save_data_to_json(file_path, data):
    if isinstance(data, str):
        data = json.loads(data)
    with open(file_path, "w") as f:
        json.dump(data, f, indent=2)


def read_data_from_json(file_path):
    with open(file_path, "r") as f:
        return json.load(f)


def draw_points_on_image(image, points, color=(0, 255, 0), radius=5):
    img = image.copy()
    for point in points:
        cv2.circle(img, tuple(point), radius, color, -1)
    return img


def draw_uvs_on_image(image, uvs, color=(0, 255, 0), radius=5):
    img = image.copy()
    H, W = img.shape[:2]
    points = [(int(uv["x"] * W), int((1 - uv["y"]) * H)) for uv in uvs]
    return draw_points_on_image(img, points, color, radius)
