# Keychron Battery Tray

## Important

This app works only with a Keychron keyboard that:

- supports QMK firmware; and
- supports wireless 2.4 GHz mode.

The keyboard must also use the required custom firmware patch.
This repository includes custom firmware only for the Keychron K8 HE.
Do not flash these files to another model. For another keyboard, you must
create and build your own firmware patch. The app may also need its USB
product IDs updated.

This app shows Keychron battery information in the Windows tray.
It does not use Bluetooth or the Keychron web launcher.

## Features

- Shows battery percentage when you point to the tray icon.
- Shows Cable mode or 2.4 GHz mode (it can maybe show Bluetooth but I didn't really bother because when on Bluetooth it reports battery life natively)
- Shows charging state when the firmware provides it.
- Shows the active HE profile: Default, Gaming, or Gamepad.
- Starts with Windows.

  <img width="533" height="137" alt="image" src="https://github.com/user-attachments/assets/70af5089-f15c-43b2-9945-78027f969c7c" />


## Requirements

- Windows x64.
- .NET 10 Desktop Runtime.
- A QMK-based Keychron keyboard with wireless 2.4 GHz support.
- The required custom firmware patch.
- A Keychron Link receiver for 2.4 GHz mode.

## Build and run

Run these commands from the repository root:

```powershell
dotnet build src/KeychronK8BatteryTray/KeychronK8BatteryTray.csproj -c Release
dotnet run --project src/KeychronK8BatteryTray/KeychronK8BatteryTray.csproj -c Release -- --self-test
dotnet publish src/KeychronK8BatteryTray/KeychronK8BatteryTray.csproj -c Release -r win-x64 --self-contained false -o publish
```

Keep `KeychronK8BatteryTray.exe` and `hidapi.dll` in the same folder.
Right-click the tray icon and select **Start with Windows**.

## Icon assets

The app embeds monochrome ICO files with 16–64 pixel sizes. It has no runtime
SVG dependency.
The source SVG files are in `assets/lucide`.

To regenerate the ICO files on Windows, run:

```powershell
.\tools\GenerateLucideIcons.ps1
```

## Check the connection

Run:

```powershell
.\publish\KeychronK8BatteryTray.exe --probe
```

Example:

```text
WiredPresent=False; Battery=100; Charging=NotCharging; Transport=Wireless24G; AnalogProfile=Gaming
```

The K8 HE uses these device IDs:

| Device | VID | PID | Usage page | Usage |
| --- | ---: | ---: | ---: | ---: |
| K8 HE over USB | `3434` | `0E80` | `FF60` | `61` |
| Keychron Link receiver | `3434` | `D030` | `FF60` | `61` |

Another model may use different IDs. Add them in
`src/KeychronK8BatteryTray/KeychronHid.cs` before you build the app.

## Firmware included in this repository

The only custom firmware in this repository is for the K8 HE.
It supports ANSI, ISO, and JIS layouts.

| Layout | QMK Toolbox file |
| --- | --- |
| ANSI / US | [`k8_he_ansi_battery_tray.bin`](firmware/k8_he_ansi_battery_tray.bin) |
| ISO | [`k8_he_iso_battery_tray.bin`](firmware/k8_he_iso_battery_tray.bin) |
| JIS | [`k8_he_jis_battery_tray.bin`](firmware/k8_he_jis_battery_tray.bin) |

Use the file that matches the physical layout. The matching `.hex` files are
also in `firmware/`. Do not flash a file for another layout. Do not flash a
keyboard image to the Keychron Link receiver.

See [`firmware/README.md`](firmware/README.md) for flashing steps and patch
details.

## Use another Keychron model

The K8 HE files are not universal files. Do not flash them to another model.
You must build a custom image for the other keyboard.

1. Save the official firmware and identify the matching QMK source revision.
2. Confirm that the model uses Keychron QMK common files, wireless battery
   code, and a raw-HID endpoint.
3. Copy the patch from
   [`firmware/keychron-k8-he-battery-wired.patch`](firmware/keychron-k8-he-battery-wired.patch)
   to the QMK source root.
4. Apply only the common-file changes:

   ```powershell
   git apply --check --exclude='keyboards/keychron/k8_he/*' keychron-k8-he-battery-wired.patch
   git apply --exclude='keyboards/keychron/k8_he/*' keychron-k8-he-battery-wired.patch
   ```

5. Set a non-zero `KC_BATTERY_MODEL_ID` in the target `config.h`.
6. Enable `WIRELESS_RAW_ENABLE` in the target `rules.mk` when required.
7. Build and test the target keymap. Flash only the keyboard.

The `0xA9` profile query is optional. It works only on models with the
Keychron analog-matrix profile code. Keep the official image for recovery.

## Tray refresh

The full battery and profile read runs once per minute. When the pointer moves
over the tray icon, the app reads the profile once per second for 15 seconds
after the last mouse movement. The profile read does not request battery data.

## Recovery and license

Use the matching official image in [`rollback/`](rollback/) for recovery.
Use Cable mode and a direct USB connection. Do not update the receiver.

The app includes HIDAPI. Its license is in
[`src/KeychronK8BatteryTray/native/HIDAPI-LICENSE.txt`](src/KeychronK8BatteryTray/native/HIDAPI-LICENSE.txt).
