# Vigil

A first-person puzzle game built in Unity for **CSC-3710 Game Design**. Explore the room, examine objects up close, and work out the codes that open what's locked.

## Getting Started

1. Install **Unity 6000.5.9f1** through Unity Hub.
2. Clone the repo:
   ```bash
   git clone https://github.com/IsaacTLowe/DCL.git
   ```
3. In Unity Hub, choose **Add → Add project from disk** and select the cloned folder.
4. Open `Assets/Scenes/MainMenu.unity` and press **Play**.

### Built with

- Universal Render Pipeline 17.5
- Input System 1.20
- uGUI and TextMesh Pro

## Controls

| Context | Input | Action |
| --- | --- | --- |
| Moving | `W` `A` `S` `D` | Move |
| | Mouse | Look |
| | `Space` | Jump |
| | `Left Shift` | Sprint |
| | Left click | Interact with what the crosshair is on |
| Keypad | Click buttons or `0`–`9` | Enter digits |
| | `Backspace` / `Delete` | Clear |
| | `Enter` | Submit code |
| | `Esc` | Close keypad |
| Lockbox door | Hold left click and drag | Swing the door open once it's unlocked |
| Inspecting an item | Drag, or `W` `A` `S` `D` / arrow keys | Rotate |
| | `Q` / `E` | Roll |
| | Scroll wheel | Zoom |
| | `R` | Reset view |
| | `Esc` or the X button | Close |

## Scenes

These are the scenes in the build, in order:

| Scene | Purpose |
| --- | --- |
| `Scenes/MainMenu` | Title screen with Play, Credits, and Quit |
| `Scenes/Demo` | Playable demo room |
| `Scenes/Credits` | Team credits |

## Scripts

All gameplay code lives in `Assets/Scripts/`.

| Script | What it does |
| --- | --- |
| `PlayerController` | First-person movement and mouse look |
| `SceneLoader` | Loads scenes and quits the game; wired to menu buttons |
| `InspectableItem` | Add it to any object with a collider so the player can click to pick it up and examine it |
| `ItemInspector` | Full-screen 3D inspect view. `InspectableItem` creates it on demand, so you don't place it in a scene |
| `LockboxKeypadController` | Opens the keypad UI and checks the entered code. The code is set by the **Access Code** field in the Inspector |
| `LockboxKeypadButton` | A single keypad button |
| `LockboxDoor` | Goes on the door's hinge object. After the keypad unlocks it, the player drags to swing the door open |

## Team

- Isaac Lowe
- Jonah DePaz
- Christian Cuellar

## Contributing

Branch off `main`, then open a pull request to merge back. To avoid scene merge conflicts, tell the team before editing a shared `.unity` file.
