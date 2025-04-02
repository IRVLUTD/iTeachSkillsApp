from pathlib import Path
from ultralytics import SAM
from utils import *


model = SAM("sam2.1_l.pt")

scene_folder = "/home/jikaiwang/GitHub/iTeachSkillsApp/Python/data/training_set/scene4"

image_file = sorted(Path(scene_folder).glob("rgb/*.png"))[-1]
prompt_file = Path("/home/jikaiwang/GitHub/iTeachSkillsApp/Python/output/prompts") / Path(scene_folder).name / "prompts.json"

with open(prompt_file) as f:
    prompts = json.load(f)["prompts"]

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
