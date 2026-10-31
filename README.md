# NPC Cards - Add New NPCs in One Click for Schedule I

> 🛟 **Need help or found a bug?** Get support at [support.doodesch.de/npccards](https://support.doodesch.de/npccards).

> Add new NPCs in one click. Browse the NPCs other players built on [NPC Vault](https://personify.doomods.com) and
> install them from your phone. Every card is a full NPC with
> look, schedule and role. Import downloaded card PNGs, send cards from the website to your game. Needs
> [Personnel](https://github.com/DooDesch-Mods/ScheduleOne-Personnel).

![Version](https://img.shields.io/badge/version-1.0.1-blue)
![Game](https://img.shields.io/badge/game-Schedule%20I-purple)
![MelonLoader](https://img.shields.io/badge/MelonLoader-0.7.3+-green)
![Sideload](https://img.shields.io/badge/Sideload-1.36.0+-yellow)
![Personnel](https://img.shields.io/badge/Personnel-2.4.2+-yellow)
![SideHustle](https://img.shields.io/badge/Side%20Hustle-optional-lightgrey)

[![MLVScan NpcCards.dll](https://mlvscan.com/attestations/att_HJkWNR9_IuLjOF43ebHTyKE3/badge.svg)](https://mlvscan.com/attestations/att_HJkWNR9_IuLjOF43ebHTyKE3)

![NPC Cards](https://raw.githubusercontent.com/DooDesch-Mods/ScheduleOne-NpcCards/main/docs/img/header.jpg)

## What it does

| Tab | |
|---|---|
| **Browse** | Search NPC Vault, filter by Customer, Dealer, Supplier or Civilian, sort by new, hearts or downloads. Open a card for its region, greeting and credit, then install it. |
| **Installed** | Every card Personnel has installed, also the ones from Personify or the import folder. Remove a card here. |
| **Import** | Card PNGs in `UserData/Personnel/Import/`. Install one now, or let Personnel install them at the next start. |
| **Account** | Connect the game to your NPC Vault account. Cards you send with "Send to game" on the website arrive here. Upload a card PNG from the import folder. |

A new NPC shows up after the next game start, not right away. The amber band says how many NPCs arrive or leave.

"Make your own" opens [Personify](https://thunderstore.io/c/schedule-i/p/DooDesch/Personify/), the editor that makes cards.

## Where it opens

- **Phone:** the NPC Cards app, in landscape and portrait.
- **Main menu:** with [Side Hustle](https://github.com/DooDesch-Mods/ScheduleOne-SideHustle) installed, NPC Cards is
  also a menu gamemode. Escape or right-click steps back.

## Online

The mod talks to `personify.doomods.com` only:

- Browsing loads card lists and card pictures. No sign-in.
- Installing downloads the card PNG.
- Connecting links this game to your account with a code you confirm on the website. Personnel keeps the token in
  `UserData/Personnel/vault.json`. Disconnect removes it.
- Uploading sends the card PNG you pick.

## Screenshots

| | |
|---|---|
| ![Browse NPC Vault](https://raw.githubusercontent.com/DooDesch-Mods/ScheduleOne-NpcCards/main/docs/img/browse.jpg) | ![Card detail](https://raw.githubusercontent.com/DooDesch-Mods/ScheduleOne-NpcCards/main/docs/img/card-detail.jpg) |
| ![Installed cards](https://raw.githubusercontent.com/DooDesch-Mods/ScheduleOne-NpcCards/main/docs/img/installed.jpg) | ![Portrait phone](https://raw.githubusercontent.com/DooDesch-Mods/ScheduleOne-NpcCards/main/docs/img/portrait.jpg) |
| ![Main-menu gamemode](https://raw.githubusercontent.com/DooDesch-Mods/ScheduleOne-NpcCards/main/docs/img/main-menu.jpg) | |

## Requirements

- MelonLoader 0.7.3+
- [Sideload](https://github.com/DooDesch-Mods/ScheduleOne-Sideload) 1.36.0+
- [Personnel](https://github.com/DooDesch-Mods/ScheduleOne-Personnel) 2.4.2+
- [S1API](https://thunderstore.io/c/schedule-i/p/ifBars/S1API_Forked/) (Personnel needs it to bring NPCs into the game)
- [Side Hustle](https://github.com/DooDesch-Mods/ScheduleOne-SideHustle) (optional, for the menu gamemode)

## Languages

English and German. Your own translation goes to `UserData/DooDesch/Localization/NpcCards/<code>.json`; the
setting "Language" in the "DooDesch" preferences category picks the language.

## License

Provided as-is under the [MIT License](LICENSE.md).

This mod is written with help from generative AI tools (Anthropic Claude, OpenAI Codex).
