from pathlib import Path
from ultralytics import SAM
from utils import *


# Usage:
#   python sam2_test.py --scene_folder <scene_dir> [--prompt_file <prompts.json>]
# Runs SAM2 on the last frame of <scene_dir>/rgb/*.png with the HoloLens point
# prompts, saves a visualization to test.png and writes the resulting boxes back
# into the prompt file under "bboxes_xyxy" (the key read by
# iTeach-UOIS/robokit/propogate_masks_via_bbox_prompt_samv2.py).
args_parser = argparse.ArgumentParser()
args_parser.add_argument("--scene_folder", type=str, required=True)
args_parser.add_argument(
    "--prompt_file",
    type=str,
    default=None,
    help="Defaults to output/prompts/<scene_name>/prompts.json",
)
args = args_parser.parse_args()

model = SAM("sam2.1_l.pt")

scene_folder = args.scene_folder

image_file = sorted(Path(scene_folder).glob("rgb/*.png"))[-1]
prompt_file = (
    Path(args.prompt_file)
    if args.prompt_file
    else CURR_DIR / "output" / "prompts" / Path(scene_folder).name / "prompts.json"
)

with open(prompt_file) as f:
    prompt_data = json.load(f)
prompts = prompt_data["prompts"]

image = cv2.imread(str(image_file))[:, :, ::-1]
H, W = image.shape[:2]
seg_boxes = []
seg_masks = []

for prompt in prompts:
    points = [(int(pt["x"] * W), int((1 - pt["y"]) * H)) for pt in prompt["points"]]
    labels = prompt["labels"]
    results = model(image, points=points, labels=labels)
    boxes = results[0].boxes
    masks = results[0].masks
    box = boxes.cpu().numpy().xyxy[0].astype(int)
    mask = masks.cpu().numpy().data[0].astype(bool)
    seg_boxes.append(box)
    seg_masks.append(mask)

vis = annotate(image, boxes=seg_boxes, masks=seg_masks)
vis = draw_prompts_on_image(vis, prompts)

vis = PILImg.fromarray(vis)
# vis.show()

vis.save("test.png")

prompt_data["bboxes_xyxy"] = [box.tolist() for box in seg_boxes]
save_data_to_json(prompt_file, prompt_data)
print(f"Wrote {len(seg_boxes)} boxes to {prompt_file}")
