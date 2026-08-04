# Keychron K8 HE Battery Tray

This project provides a small Windows tray app for the Keychron K8 HE.

The app reads the keyboard battery level over the Keychron Link 2.4 GHz
receiver. It also detects the direct USB keyboard connection.

## Features

- Show the battery level in the tray tooltip.
- Show the 2.4 GHz or wired state.
- Show USB power when the keyboard is wired.
- Start with Windows.
- Use no Keychron app or Bluetooth connection.

## Requirements

- Windows x64.
- .NET 10 Desktop Runtime.
- A K8 HE with the custom firmware in this repository.
- The Keychron Link receiver for 2.4 GHz readings.

The app does not report charge current. It uses the direct USB connection as
the USB power signal. A wired keyboard can be full, so this signal does not
prove that the battery is still charging.

## Build

Run these commands from the repository root:

```powershell
dotnet build src/KeychronK8BatteryTray/KeychronK8BatteryTray.csproj -c Release
dotnet run --project src/KeychronK8BatteryTray/KeychronK8BatteryTray.csproj -c Release -- --self-test
dotnet publish src/KeychronK8BatteryTray/KeychronK8BatteryTray.csproj -c Release -r win-x64 --self-contained false -o publish
```

The publish folder contains the app and `hidapi.dll`. Keep these files in the
same folder.

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
WiredPresent=False; WirelessBattery=91
```

The app uses these device values:

| Device | VID | PID | Usage page | Usage |
| --- | ---: | ---: | ---: | ---: |
| K8 HE over USB | `3434` | `0E80` | `FF60` | `61` |
| Keychron Link receiver | `3434` | `D030` | `FF60` | `61` |

The battery request is a 33-byte HID report:

```text
request:  00 A4 00 00 ... 00
response: A4 PP 00 00 ... 00
```

`PP` is a value from 0 to 100. The response can also include a leading zero
report ID. The app accepts both response forms.

The query is read-only. It returns the firmware's cached battery value. The
firmware updates this value during wireless operation.

## Tray states

| State | Tooltip example |
| --- | --- |
| 2.4 GHz | `Keychron K8 HE: 91% - 2.4 GHz` |
| 2.4 GHz with USB power | `Keychron K8 HE: 91% - 2.4 GHz - USB power` |
| Wired | `Keychron K8 HE: Wired - USB power` |
| Not detected | `Keychron K8 HE: Not detected - last seen 91%` |

The app checks the receiver once per minute. Moving the pointer over the icon
can request an earlier check. Use **Refresh now** for an immediate check.

## Firmware files

The custom firmware adds a read-only raw-HID command to the K8 HE.

| Layout | Custom firmware |
| --- | --- |
| ANSI / US | [`firmware/k8_he_ansi_battery_rawhid.bin`](firmware/k8_he_ansi_battery_rawhid.bin) |
| ISO | [`firmware/k8_he_iso_battery_rawhid.bin`](firmware/k8_he_iso_battery_rawhid.bin) |
| JIS | [`firmware/k8_he_jis_battery_rawhid.bin`](firmware/k8_he_jis_battery_rawhid.bin) |

Use the file that matches the physical keyboard layout. Do not flash a file
for another layout. The `.bin` file is the recommended file for QMK Toolbox.

The matching official firmware files are in [`rollback/`](rollback/).

The source patch is [`keychron-k8-he-battery.patch`](keychron-k8-he-battery.patch).
It changes only the keyboard firmware. It does not change the receiver
firmware, key behavior, battery math, or Bluetooth behavior.

## Recovery

If the custom firmware causes a problem, flash the matching file from
[`rollback/`](rollback/) with QMK Toolbox.

Use Cable mode and a direct USB connection for recovery. Do not update the
receiver for this operation.

## Third-party software

The app includes HIDAPI. Its license is in
[`src/KeychronK8BatteryTray/native/HIDAPI-LICENSE.txt`](src/KeychronK8BatteryTray/native/HIDAPI-LICENSE.txt).
