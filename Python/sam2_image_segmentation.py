import torch
import numpy as np
import torch
import matplotlib.pyplot as plt
from PIL import Image
from sam2.build_sam import build_sam2
from sam2.sam2_image_predictor import SAM2ImagePredictor

SAM2_CKP = "/home/jikaiwang/GitHub/sam2/checkpoints/sam2.1_hiera_large.pt"
MODEL_CFG = "/home/jikaiwang/GitHub/sam2/configs/sam2.1/sam2.1_hiera_l.yaml"
np.random.seed(3)


class Sam2ImageSegmentation:
    def __init__(self, device="cuda"):
        self._device = device
        # use bfloat16 for the entire notebook
        torch.autocast(self._device, dtype=torch.bfloat16).__enter__()
        # turn on tfloat32 for Ampere GPUs (https://pytorch.org/docs/stable/notes/cuda.html#tensorfloat-32-tf32-on-ampere-devices)
        if torch.cuda.get_device_properties(0).major >= 8:
            torch.backends.cuda.matmul.allow_tf32 = True
            torch.backends.cudnn.allow_tf32 = True

        self._predictor = self._init_predictor()

    def _init_predictor(self):
        sam2_model = build_sam2(MODEL_CFG, SAM2_CKP, device=self._device)
        predictor = SAM2ImagePredictor(sam2_model)
        return predictor

    def _get_mask(self, input_point, input_label):
        input_point = np.array([input_point])
        input_label = np.array([1])
        masks, scores, logits = self._predictor.predict(
            point_coords=input_point,
            point_labels=input_label,
            multimask_output=True,
        )
        sorted_ind = np.argmax(scores)
        masks = masks[sorted_ind]
        return masks[0]

    def predict(self, img_rgb, prompts):
        H, W = img_rgb.shape[:2]
        self._predictor.set_image(img_rgb)
        masks = []
        for prompt in prompts:
            mask = self._get_mask([prompt[0]*W, prompt[1]*H], 1)
            masks.append(mask)
        return masks
