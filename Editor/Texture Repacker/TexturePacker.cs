using UnityEngine;
using System.Linq;

namespace RexTools.TextureRepacker.Editor
{
    public class ChannelSlotData
    {
        public Texture2D texture;
        public int channelIndex = 0; // 0=R, 1=G, 2=B, 3=A
        public bool invert = false;
        public bool useCustom = false;
        public float customValue = 0.5f;
    }

    public static class TexturePacker
    {
        public static Texture2D Pack(
            ChannelSlotData[] slots,
            int width,
            int height,
            System.Action<string, float> progressCallback = null)
        {
            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];

            Color[][] slotPixels = new Color[4][];
            int[] slotW = new int[4];
            int[] slotH = new int[4];
            float[] customVals = new float[4];

            for (int c = 0; c < 4; c++)
            {
                if (!slots[c].useCustom && slots[c].texture != null)
                {
                    slotPixels[c] = TextureRepackerUtils.GetReadablePixels(slots[c].texture);
                    slotW[c] = slots[c].texture.width;
                    slotH[c] = slots[c].texture.height;
                }
                if (slots[c].useCustom)
                    customVals[c] = slots[c].invert ? 1f - slots[c].customValue : slots[c].customValue;
                else if (slots[c].texture == null)
                    customVals[c] = (c == 3) ? 1f : 0f;
            }

            int progressInterval = Mathf.Max(1, height / 20);
            for (int y = 0; y < height; y++)
            {
                if (y % progressInterval == 0 && progressCallback != null)
                {
                    progressCallback($"Processing row {y}/{height}...", (float)y / height);
                }

                for (int x = 0; x < width; x++)
                {
                    float[] channels = new float[4];
                    for (int c = 0; c < 4; c++)
                    {
                        if (slots[c].useCustom || slots[c].texture == null)
                        {
                            channels[c] = customVals[c];
                        }
                        else
                        {
                            int srcX = Mathf.Clamp(x * slotW[c] / width, 0, slotW[c] - 1);
                            int srcY = Mathf.Clamp(y * slotH[c] / height, 0, slotH[c] - 1);
                            Color p = slotPixels[c][srcY * slotW[c] + srcX];
                            float val = slots[c].channelIndex == 0 ? p.r : slots[c].channelIndex == 1 ? p.g : slots[c].channelIndex == 2 ? p.b : p.a;
                            if (slots[c].invert) val = 1f - val;
                            channels[c] = val;
                        }
                    }
                    pixels[y * width + x] = new Color(channels[0], channels[1], channels[2], channels[3]);
                }
            }

            result.SetPixels(pixels);
            result.Apply();
            return result;
        }

        public static float[] SampleSlotChannel(ChannelSlotData slot, int size)
        {
            int totalPixels = size * size;
            float[] result = new float[totalPixels];

            if (slot.useCustom)
            {
                float val = slot.invert ? 1f - slot.customValue : slot.customValue;
                System.Array.Fill(result, val);
                return result;
            }

            if (slot.texture == null)
            {
                float val = (slot.channelIndex == 3) ? 1f : 0f;
                System.Array.Fill(result, val);
                return result;
            }

            Color[] srcPixels = TextureRepackerUtils.GetReadablePixels(slot.texture);
            int srcW = slot.texture.width;
            int srcH = slot.texture.height;
            int channel = slot.channelIndex;
            bool invert = slot.invert;

            for (int y = 0; y < size; y++)
            {
                int srcY = Mathf.Clamp(y * srcH / size, 0, srcH - 1);
                for (int x = 0; x < size; x++)
                {
                    int srcX = Mathf.Clamp(x * srcW / size, 0, srcW - 1);
                    Color p = srcPixels[srcY * srcW + srcX];
                    float val = channel == 0 ? p.r : channel == 1 ? p.g : channel == 2 ? p.b : p.a;
                    if (invert) val = 1f - val;
                    result[y * size + x] = val;
                }
            }
            return result;
        }
    }
}
