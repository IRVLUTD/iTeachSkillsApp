import argparse
import random
import shutil
import json
import cv2
import numpy as np
from tqdm import tqdm
from pathlib import Path
import concurrent.futures
import supervision as sv
from PIL import Image as PILImg, ImageDraw


CURR_DIR = Path(__file__).resolve().parent


def make_clean_folder(folder):
    dir = Path(folder)
    if Path(folder).exists():
        shutil.rmtree(dir)
    dir.mkdir(exist_ok=True, parents=True)


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


def draw_prompts_on_image(image, prompts, radius=3):
    img = image.copy()
    H, W = img.shape[:2]
    for idx, prompt in enumerate(prompts):
        text = f"{idx+1}"
        points = [(int(pt["x"] * W), int((1 - pt["y"]) * H)) for pt in prompt["points"]]
        labels = prompt["labels"]
        for point, label in zip(points, labels):
            color = (0, 255, 0) if label == 1 else (0, 0, 255)
            cv2.circle(img, tuple(point), radius, color, -1)
            cv2.putText(
                img, text, tuple(point), cv2.FONT_HERSHEY_SIMPLEX, 0.6, color, 2
            )
    return img


def annotate(image_source, boxes=None, masks=None):
    # Prepare detections with optional boxes and masks
    if boxes is not None:
        labels = np.arange(len(boxes))
    elif masks is not None:
        labels = np.arange(len(masks))
    else:
        labels = None

    detections = sv.Detections(
        xyxy=np.asarray(boxes) if boxes is not None else None,
        mask=np.asarray(masks) if masks is not None else None,
        class_id=labels,
    )

    # Annotators
    annotators = []
    if boxes is not None:
        annotators.append(sv.BoxAnnotator())
    if masks is not None:
        annotators.append(sv.MaskAnnotator())

    # Apply all annotators in sequence
    annotated_image = image_source.copy()
    for annotator in annotators:
        annotated_image = annotator.annotate(
            scene=annotated_image, detections=detections
        )

    return annotated_image


def draw_mask(mask, draw, random_color=False):
    """
    Draw a segmentation mask on an image.

    Parameters:
    - mask (numpy.ndarray): The segmentation mask as a NumPy array. [HxW]
    - draw (PIL.ImageDraw.ImageDraw): The PIL ImageDraw object to draw on.
    - random_color (bool, optional): Whether to use a random color for the mask. Default is False.

    Returns:
    - None
    """
    if len(mask.shape) > 2:
        mask = mask.squeeze()

    try:
        # Define the color for the mask
        if random_color:
            color = (
                random.randint(0, 255),
                random.randint(0, 255),
                random.randint(0, 255),
                153,
            )
        else:
            color = (30, 144, 255, 153)

        # Get the coordinates of non-zero elements in the mask
        nonzero_coords = np.transpose(np.nonzero(mask))

        # Draw each non-zero coordinate on the image
        for coord in nonzero_coords:
            draw.point(coord[::-1], fill=color)

    except Exception as e:
        raise e


def overlay_masks(image, masks):  # type: ignore
    """
    Overlay segmentation masks on the input image.

    Parameters:
    - image_pil (PIL.Image): The input image as a PIL image.
    - masks (List[Tensor]): List of segmentation masks as torch Tensors.

    Returns:
    - PIL.Image: The image with overlayed segmentation masks.
    """
    try:
        image_pil = PILImg.fromarray(image)
        mask_image = PILImg.new("RGBA", image_pil.size, color=(0, 0, 0, 0))  # type: ignore
        mask_draw = ImageDraw.Draw(mask_image)

        for mask in np.asarray(masks):
            draw_mask(mask, mask_draw, random_color=True)

        image_pil = image_pil.convert("RGBA")
        image_pil.alpha_composite(mask_image)
        image_pil.convert("RGB")
        return np.asarray(image_pil)

    except Exception as e:
        raise e
