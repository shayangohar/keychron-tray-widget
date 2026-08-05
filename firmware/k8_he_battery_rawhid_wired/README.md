# Keychron K8 HE battery raw-HID firmware (wired-aware)

This is the second K8 HE firmware patch. It keeps the working `0xA4` battery
query and adds fresh battery sampling while the keyboard is in USB cable mode,
plus charging state, voltage, transport, and model metadata.

## Images

Use the image matching the physical keyboard layout:

| Layout | Firmware |
| --- | --- |
| ANSI / US | `k8_he_ansi_battery_rawhid_wired.bin` |
| ISO | `k8_he_iso_battery_rawhid_wired.bin` |
| JIS | `k8_he_jis_battery_rawhid_wired.bin` |

The `.bin` files are the recommended files for QMK Toolbox. Matching `.hex`
files are included for archival purposes. SHA-256 values are in
`SHA256SUMS.txt`.

## A4 report

Send a 32-byte raw-HID payload beginning with `A4`:

```text
request:  A4 00 00 ... 00
response: A4 PP VL VH CS TR MI 00 ... 00
```

| Byte | Meaning |
| ---: | --- |
| 0 | `A4` battery command |
| 1 | Battery percentage, 0–100 |
| 2–3 | Battery voltage in mV, little-endian |
| 4 | Charging state: `0` not charging, `1` charging, `2` full |
| 5 | Transport: `1` USB, `2` Bluetooth, `4` 2.4 GHz |
| 6 | Model ID: `2` for K8 HE |

The report is read-only. Battery sampling remains on the keyboard; the host
does not control charging or the wireless module.

## Flashing

1. Switch the keyboard to **Cable** mode and connect it directly by USB.
2. Close VIA, Keychron Launcher, and the tray app so no program has the HID
   interface open.
3. Open QMK Toolbox and load the matching `.bin` file above.
4. Enter bootloader mode: disconnect USB, keep the switch on **Cable**, hold
   **Esc** (or the reset button underneath the space bar), then reconnect USB.
5. Confirm QMK Toolbox shows the STM32 DFU device and flash the image.
6. Wait for completion, disconnect/reconnect USB, and switch back to 2.4 GHz.

Do not flash an image for another layout. Do not update the Keychron Link
receiver; this patch changes only the keyboard firmware.

## Source

`keychron-k8-he-battery-wired.patch` applies to Keychron QMK commit
`07bfc38a4b11b8dac7ab758dfc5868b4229499ca` (`2025q3`).

## Use this patch for another model

The binary files in this folder are for the K8 HE only. Do not flash them to
another keyboard. The patch shows the common QMK changes. It also contains
two K8 HE files:

- `keyboards/keychron/k8_he/config.h` sets model ID `2`.
- `keyboards/keychron/k8_he/rules.mk` enables `WIRELESS_RAW_ENABLE`.

For another Keychron QMK keyboard:

1. Check out the QMK source revision used by that keyboard.
2. Skip the K8 HE files when you apply the patch. Run this command from the
   QMK source root:

   ```powershell
   git apply --check --exclude='keyboards/keychron/k8_he/*' keychron-k8-he-battery-wired.patch
   git apply --exclude='keyboards/keychron/k8_he/*' keychron-k8-he-battery-wired.patch
   ```

3. Review the common-file changes before you build.
4. Set a non-zero `KC_BATTERY_MODEL_ID` in the target keyboard `config.h`.
5. Enable `WIRELESS_RAW_ENABLE` in the target `rules.mk` when required.
6. Build the target keymap. Flash only the keyboard.
7. Test Cable mode and 2.4 GHz mode with the tray app `--probe` command.

Do not force the patch when the source code is different. Do not flash the K8
HE images to another model. Keep the official firmware for recovery.

The `0xA9` profile query is optional. It works only when the target has the
Keychron analog-matrix profile code. The battery query works without a profile
query.
