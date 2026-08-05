# Keychron K8 HE custom firmware

This repository contains custom firmware only for the Keychron K8 HE.
Do not flash these files to another model.

## Files

| Layout | QMK Toolbox file |
| --- | --- |
| ANSI / US | `k8_he_ansi_battery_tray.bin` |
| ISO | `k8_he_iso_battery_tray.bin` |
| JIS | `k8_he_jis_battery_tray.bin` |

Use the file that matches the physical keyboard layout. The `.bin` files are
recommended for QMK Toolbox. Matching `.hex` files are also included.

## What the patch adds

- An extended `0xA4` battery report.
- Battery sampling while the keyboard is in Cable mode.
- Charging state and transport data.
- Raw-HID requests over the 2.4 GHz receiver.

The report contains battery voltage. The app does not display voltage.
The app uses `0xA9 0x10` to read the active HE profile.

## Flash the firmware

1. Set the keyboard to **Cable** mode. Connect it directly by USB.
2. Close VIA, Keychron Launcher, and the tray app.
3. Open QMK Toolbox. Load the `.bin` file for the physical layout.
4. Enter bootloader mode. Disconnect USB, hold **Esc** or the reset button,
   and reconnect USB.
5. Confirm that QMK Toolbox shows the STM32 DFU device. Flash the file.
6. Wait for the flash to finish. Reconnect USB and select 2.4 GHz mode.

Do not flash a file for another layout. Do not flash the Keychron Link
receiver.

## A4 report

HIDAPI sends a 33-byte report. Byte 0 is the report ID.

```text
request:  00 A4 00 00 ... 00
response: A4 PP VL VH CS TR MI 00 ... 00
```

| Byte | Meaning |
| ---: | --- |
| 0 | `A4` battery command |
| 1 | Battery percentage, from 0 to 100 |
| 2-3 | Battery voltage in mV, little-endian |
| 4 | `0` not charging, `1` charging, `2` full |
| 5 | `1` USB, `2` Bluetooth, `4` 2.4 GHz |
| 6 | Model ID. `2` is the K8 HE. |

The report is read-only. It does not change charging or the wireless mode.

## Source patch

`keychron-k8-he-battery-wired.patch` applies to Keychron QMK commit
`07bfc38a4b11b8dac7ab758dfc5868b4229499ca` (`2025q3`).

## Another Keychron model

The K8 HE binaries are not universal. For another model, create and build your
own firmware patch and image. Do not flash a K8 HE file.

1. Check out the QMK source revision for the target keyboard.
2. Copy the patch to the QMK source root.
3. Apply the common-file changes:

   ```powershell
   git apply --check --exclude='keyboards/keychron/k8_he/*' keychron-k8-he-battery-wired.patch
   git apply --exclude='keyboards/keychron/k8_he/*' keychron-k8-he-battery-wired.patch
   ```

4. Set a non-zero `KC_BATTERY_MODEL_ID` in the target `config.h`.
5. Enable `WIRELESS_RAW_ENABLE` in the target `rules.mk` when required.
6. Build and test the target keymap. Flash only the keyboard.

The `0xA9` profile query is optional. It works only when the target has the
Keychron analog-matrix profile code. Keep the official firmware for recovery.
