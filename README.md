# Keychron Battery Tray

This project provides a small Windows tray app for Keychron keyboards.
The K8 HE is the tested model.

The app reads the battery report from the keyboard. It works in Cable mode
and through the Keychron Link 2.4 GHz receiver. It does not use Bluetooth or
the Keychron application.

## Features

- Show the battery percentage when you point to the tray icon.
- Show the current transport: Wired, 2.4 GHz, or Bluetooth.
- Show the charge state when the firmware provides it.
- Show the active HE analog profile by name.
- Use a color battery icon and a charging bolt for Cable mode or charging.
- Start with Windows.
- Use a read-only HID request.

## Requirements

- Windows x64.
- .NET 10 Desktop Runtime.
- A Keychron keyboard with the wired-aware firmware from this repository.
- The Keychron Link receiver for 2.4 GHz readings.

The included firmware files are for the K8 HE. Do not flash them to another
model. See [Patch another model](#patch-another-model) before you build
firmware for another keyboard.

The app does not need a Bluetooth connection. The firmware reports the
transport and charge state. Cable mode reports the battery value over the
direct USB HID endpoint.

## Build

Run these commands from the repository root:

```powershell
dotnet build src/KeychronK8BatteryTray/KeychronK8BatteryTray.csproj -c Release
dotnet run --project src/KeychronK8BatteryTray/KeychronK8BatteryTray.csproj -c Release -- --self-test
dotnet publish src/KeychronK8BatteryTray/KeychronK8BatteryTray.csproj -c Release -r win-x64 --self-contained false -o publish
```

The `publish` folder contains the app and `hidapi.dll`. Keep these files in
the same folder.

Run the app from a stable folder, such as:

```text
%LocalAppData%\KeychronK8Battery
```

Right-click the tray icon and select **Start with Windows**.

The app writes one value under the current user's Windows Run key. It does not
need administrator access.

## Check the HID connection

Run the one-shot probe:

```powershell
publish/KeychronK8BatteryTray.exe --probe
```

Example output:

```text
WiredPresent=True; Battery=100; Charging=Charging; Transport=Usb; AnalogProfile=Gaming
```

The app checks these device IDs:

```text
Keyboard: 3434:0E80
Receiver: 3434:D030
```

If another model uses a different product ID, add the ID in
`src/KeychronK8BatteryTray/KeychronHid.cs` and build the app again. Do not
guess a product ID.

The app uses these device values:

| Device | VID | PID | Usage page | Usage |
| --- | ---: | ---: | ---: | ---: |
| K8 HE over USB | `3434` | `0E80` | `FF60` | `61` |
| Keychron Link receiver | `3434` | `D030` | `FF60` | `61` |

The app checks the direct USB endpoint first. If it does not return a report,
the app checks the receiver.

## HID commands

The wired-aware firmware accepts a 33-byte HID report. The first byte is the
report ID used by HIDAPI.

### Battery: `0xA4`

Request:

```text
00 A4 00 00 ... 00
```

Response:

```text
A4 PP VL VH CS TR MI 00 ... 00
```

The response can have a leading zero report ID. The fields are:

| Field | Meaning |
| --- | --- |
| `PP` | Battery percentage, from 0 to 100. |
| `VL VH` | Battery voltage in millivolts, little-endian. The app does not display this field. |
| `CS` | `0` not charging, `1` charging, `2` full. |
| `TR` | `1` USB, `2` Bluetooth, `4` 2.4 GHz. |
| `MI` | Non-zero model ID. `2` is the K8 HE. |

The app accepts any non-zero model ID in the extended report. The request is
read-only. It does not change charging or the wireless mode.

### HE profile: `0xA9`

Request:

```text
00 A9 10 00 ... 00
```

Response:

```text
A9 10 PI PC 00 ... 00
```

`PI` is the zero-based active profile index. `PC` is the profile count. The
K8 HE reports three profiles. The app uses these names:

| Index | Name |
| ---: | --- |
| `0` | Default |
| `1` | Gaming |
| `2` | Gamepad |

## Tray states

| State | Tooltip example |
| --- | --- |
| Wired and charging | `Keychron: 100% - Wired - Charging - Profile: Gaming` |
| Wired and full | `Keychron: 100% - Wired - Full - Profile: Gaming` |
| 2.4 GHz | `Keychron: 91% - 2.4 GHz - Profile: Default` |
| Not detected | `Keychron: Not detected - last seen 91% - Profile: Default` |

The app checks battery data once per minute. The normal profile check also
runs once per minute as part of that full check. When the pointer moves over
the tray icon, the app sends an `0xA9` profile-only request once per second for
15 seconds after the last mouse movement. This request does not request
battery data. Moving the pointer
over the icon can also request an earlier full check. Use **Refresh now** for
an immediate full check.

`NotifyIcon` does not provide a reliable mouse-leave event. The app therefore
uses the 15-second window as the hover session. The current firmware does not
send a profile-change event. A firmware change could send an event and remove
this timer.

## Patch another model

The included patch is a K8 HE example. It is not a universal firmware image.
Use the steps below for another Keychron QMK keyboard with 2.4 GHz support.

1. Confirm that the keyboard uses Keychron QMK common files. It must use the
   Keychron raw-HID code, wireless battery code, and a raw-HID endpoint.
2. Save the official firmware. Save the QMK source revision that matches the
   official firmware. Do not use the K8 HE binary.
3. Apply the common-file changes from
   [`keychron-k8-he-battery-wired.patch`](firmware/k8_he_battery_rawhid_wired/keychron-k8-he-battery-wired.patch).
   Run the commands from the QMK source root. Copy the patch there, or give
   `git apply` its full path. Check the patch first:

   ```powershell
   git apply --check --exclude='keyboards/keychron/k8_he/*' keychron-k8-he-battery-wired.patch
   git apply --exclude='keyboards/keychron/k8_he/*' keychron-k8-he-battery-wired.patch
   ```

4. Keep the `0xA4` handler in `keychron_raw_hid.c`. Set a non-zero,
   model-specific `KC_BATTERY_MODEL_ID` in the target keyboard `config.h`.
   The model ID is metadata. It does not change the battery measurement.
5. Enable `WIRELESS_RAW_ENABLE` in the target keyboard `rules.mk` when the
   model needs raw-HID requests over the 2.4 GHz receiver. Do not copy this
   step if the target already uses the correct option.
6. Build the target keymap with QMK. Example:

   ```powershell
   qmk compile -kb keychron/<model> -km <keymap>
   ```

   Replace `<model>` and `<keymap>` with values for the target keyboard.
7. Test the new image in Cable mode. Then test the same image through the
   2.4 GHz receiver. Run `--probe` and confirm the battery, transport, and
   charge fields.
8. Flash only the keyboard. Do not flash the Keychron Link receiver. Keep the
   official image for recovery.

The patch can fail when the QMK source revision is different. Use the exact
source revision first. If the patch still fails, review each hunk against the
target model. Do not force a patch that changes unrelated wireless code.

The `0xA9` profile query is optional. It works for HE models that include the
analog-matrix profile code. Other models can use the battery and transport
parts of the app without reporting a profile.

## Firmware files

For this app, use the wired-aware files in
[`firmware/k8_he_battery_rawhid_wired/`](firmware/k8_he_battery_rawhid_wired/):

| Layout | Firmware file |
| --- | --- |
| ANSI / US | [`k8_he_ansi_battery_rawhid_wired.bin`](firmware/k8_he_battery_rawhid_wired/k8_he_ansi_battery_rawhid_wired.bin) |
| ISO | [`k8_he_iso_battery_rawhid_wired.bin`](firmware/k8_he_battery_rawhid_wired/k8_he_iso_battery_rawhid_wired.bin) |
| JIS | [`k8_he_jis_battery_rawhid_wired.bin`](firmware/k8_he_battery_rawhid_wired/k8_he_jis_battery_rawhid_wired.bin) |

Use the file that matches the physical keyboard layout. Do not flash a file
for another layout. The `.bin` file is the recommended file for QMK Toolbox.

The folder also contains the matching `.hex` files, SHA-256 checksums, the
source patch, and the firmware README.

The older firmware files in `firmware/` report only the battery percentage.
They do not provide the wired-mode fields used by the current app.

## Recovery

If the custom firmware causes a problem, flash the matching official file from
[`rollback/`](rollback/) with QMK Toolbox.

Use Cable mode and a direct USB connection for recovery. Do not update the
receiver for this operation.

## Third-party software

The app includes HIDAPI. Its license is in
[`src/KeychronK8BatteryTray/native/HIDAPI-LICENSE.txt`](src/KeychronK8BatteryTray/native/HIDAPI-LICENSE.txt).
