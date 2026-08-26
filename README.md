<div align="center">

<img src="docs/images/logo.png" width="128" alt="Cobalt.Fluent" />

# Cobalt.Fluent

**A Windows 11 Fluent implementation for Avalonia**

62 controls · 11 groups · light and dark themes · zero third-party dependencies · industrial HMI control set included

[![build](https://github.com/RoorJiaMo/CobaltFluent/actions/workflows/build.yml/badge.svg)](https://github.com/RoorJiaMo/CobaltFluent/actions/workflows/build.yml)
[![license](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Avalonia](https://img.shields.io/badge/Avalonia-11.3-8b5cf6.svg)](https://avaloniaui.net)
[![.NET](https://img.shields.io/badge/.NET-8.0-512bd4.svg)](https://dotnet.microsoft.com)

**English** · [简体中文](README.zh-CN.md)

<img src="docs/images/gallery-dark.png" width="880" alt="Cobalt.Fluent gallery — industrial HMI readouts" />

</div>

---

## Overview

Cobalt.Fluent implements the Windows 11 Fluent visual specification in full on Avalonia 11.3, and adds a dedicated control set built for process-control interfaces.

General-purpose control libraries fall short in industrial HMI applications in recurring ways: numeric updates cause layout jitter, alarms rely on flashing, jog buttons have incomplete stop conditions, and parameter writes echo the entered value rather than the value the device actually accepted. These are safety concerns, not cosmetic ones. This library implements the corresponding safety semantics inside the controls and pins them with regression tests.

<table>
<tr>
<td width="25%" valign="top">

### Zero third-party dependencies

References only Avalonia itself, `Avalonia.Themes.Fluent` and `Avalonia.Controls.DataGrid` — all first-party packages. Charts are custom-drawn; icons are built-in vector paths.

</td>
<td width="25%" valign="top">

### Verifiable specification

Most controls ship a state matrix. CI renders all 49 gallery sections headlessly into 98 screenshots; a render failure fails the build.

</td>
<td width="25%" valign="top">

### Built for embedded targets

No dependency on the Segoe Fluent Icons font. Motion is limited to transform, opacity and brush transitions — nothing animates layout. Acrylic and shimmer effects can be turned off. The control layer is free of reflection bindings, so it survives `TrimMode=full` and NativeAOT.

</td>
<td width="25%" valign="top">

### Tested safety semantics

E-stop latching, multi-source jog stop, event-driven heartbeat and stale-value retention are all covered by regression tests.

</td>
</tr>
</table>

---

## Getting started

```bash
dotnet add package Cobalt.Fluent
```

```xml
<!-- App.axaml -->
<Application xmlns="https://github.com/avaloniaui"
             xmlns:fc="using:Cobalt.Fluent">
  <Application.Styles>
    <fc:CobaltFluentTheme />
  </Application.Styles>
</Application>
```

Switch themes through `Application.Current.RequestedThemeVariant` (`Light` / `Dark` / `Default`).
Controls added by this library live in the `Cobalt.Fluent.Controls` namespace:

```xml
xmlns:fc="using:Cobalt.Fluent.Controls"
```

```xml
<fc:Readout Label="Chamber temperature" Value="85.5" Unit="°C"
            Setpoint="85.0" Tolerance="1.0" Size="Large" />

<fc:JogButton Content="Jog X+" StopCommand="{Binding StopAxis}"
              WatchdogTimeout="0:0:0.5" />

<fc:TrendChart Series="{Binding Channels}" IsTrackballEnabled="True" />
```

---

## Gallery

The gallery application contains 49 sections. A control section is laid out as the visual specification, an interactive demo, and a reference table of resource keys and pseudo-classes; 22 of them also freeze every pseudo-class side by side in a state matrix. Five further sections — the design baseline, typography, the icon list, and the introductions to groups 7 and 9 — carry prose and reference material rather than a control demo.

<table>
<tr>
<td width="50%"><img src="docs/images/gallery-light.png" alt="Light theme — trend chart" /></td>
<td width="50%"><img src="docs/images/gallery-dark.png" alt="Dark theme — HMI readouts" /></td>
</tr>
<tr>
<td align="center"><sub>Light theme · Group 9, Charts</sub></td>
<td align="center"><sub>Dark theme · Group 7, Industrial HMI</sub></td>
</tr>
</table>

Every page offers **View source**: the sample XAML and C# for the current page, plus the `ControlTheme` and control class as they exist in the library — with line numbers, syntax highlighting and one-click copy.

<img src="docs/images/source-viewer.png" width="720" alt="Source viewer — sample code alongside the ControlTheme" />

```bash
dotnet run --project samples/Cobalt.Fluent.Gallery
```

---

## Control coverage

11 groups. Baseline desktop control height is 32px.

<details>
<summary><b>Full list (62 controls)</b></summary>

<br>

| Group | Controls | Origin |
|---|---|---|
| **2** Basic input | Button · ToggleButton · SplitButton · DropDownButton · HyperlinkButton · TextBox · NumberBox · ComboBox · CheckBox · RadioButton · ToggleSwitch · Slider | Avalonia built-ins re-themed by this library |
| **3** Containers | Card · SettingsCard · SettingsGroup · Expander · TabControl · TabView · NavigationView | Card / SettingsCard / SettingsGroup / TabView / NavigationView implemented here |
| **4** Collections | ListBox · DataGrid · TreeView | ListBoxItem template rewritten (adds the selection indicator bar) |
| **5** Feedback | InfoBar · InfoBadge · ProgressBar · ProgressRing · ToolTip | InfoBar / InfoBadge / ProgressRing implemented here |
| **6** Popups | Flyout · MenuFlyout · ContentDialog · TeachingTip · CommandBar | ContentDialog / TeachingTip / CommandBar implemented here |
| **7** Industrial HMI | Readout · StatusIndicator · AlarmBanner · ParameterRow · JogButton · EStopButton · DeviceStatusBar · NumericKeypad | All implemented here |
| **8** Date and time | CalendarDatePicker · TimePicker · Calendar · RangeCalendar | Built-ins re-themed; RangeCalendar implemented here (adds range-endpoint pseudo-classes). A date range is composed from two `CalendarDatePicker`s — there is no `DateRangePicker` type. |
| **9** Charts | ChartFrame · TrendChart · Gauge · BarChart · Sparkline · ChartLegend | All drawn directly by this library |
| **10** Data grid extras | DataGridToolbar · Pagination · EmptyState · Skeleton | All implemented here |
| **11** Common additions | AutoSuggestBox · BreadcrumbBar · SegmentedControl · Chip · Stepper · GridSplitter · Toast · PersonPicture | All implemented here, except AutoSuggestBox (Avalonia's built-in `AutoCompleteBox`, re-themed) and GridSplitter |

Full API reference in [`docs/CONTROLS.md`](docs/CONTROLS.md) (77 types, extracted from source by script).

</details>

---

## Design baseline

Four rules run through the whole library. Any single control can be drawn in several defensible ways; once many controls share a screen, only a consistent treatment of layering, corner radius, shadow and font weight keeps the visual order readable.

| Rule | Detail |
|---|---|
| **Two layers only** | A base layer (window background, carrying navigation and the command bar) and a content layer. `Card` partitions the content layer; it does not introduce a third layer. |
| **Corner radius is 8 / 4 / 0 only** | 8 for popup surfaces, 4 for controls, 0 where elements meet. The exceptions are shapes that are round by definition — the calendar day cell, the ToggleSwitch track, the StatusIndicator and heartbeat dots, Chip, PersonPicture and the E-stop knob — and the selection indicator bars, which are rounded to their own width. |
| **Shadows only on floating surfaces** | ToolTip / Flyout / MenuFlyout / ContentDialog / TeachingTip / Toast, plus the ComboBox, suggestion-list and date-picker popups. The one in-page exception is the `EStopButton` knob, where a raised shadow that flattens on press and turns inset when latched is the affordance. Everything else inside a page is separated by strokes. |
| **Font weight is 400 / 600 only** | No Bold and no italics — CJK has no native italic, and synthesised obliques read poorly at small sizes. |

The control layer contains no literal colour values except in `BoxShadow`, whose syntax takes colours rather than brushes; every other colour resolves through `DynamicResource` to a token defined in the token layer. Changing the accent colour, adding a high-contrast variant or theming per product line means editing the token layer only, leaving the control layer untouched.

---

## Industrial HMI controls (group 7)

This group has no counterpart in WinUI or FluentAvalonia. The constraints below concern personnel and equipment safety; each is implemented inside the control and pinned by regression tests.

<details>
<summary><b>Seven safety constraints</b></summary>

<br>

- **Safety colours are not status colours.** Emergency stop and Alarm-severity banners use `SafetyRedBrush` rather than `SystemFillColorCriticalBrush` — the latter resolves to a pale pink `#FF99A4` in dark theme, which is wrong for a severity that demands immediate action. `SafetyRed` is tuned per theme for contrast (`#C42B1C` light, `#E81123` dark), but stays an unmistakable red in both.
- **Alarms breathe rather than flash** (1.5s, opacity 1↔.62). High-frequency flashing causes visual fatigue and carries a photosensitive-epilepsy risk. When animation is disabled, a safety-yellow stroke is added automatically so Alarm and Warning stay distinguishable in the degraded state.
- **`JogButton` has seven stop triggers** — release, pointer capture lost, pointer exit, focus lost, key up, control detached, and watchdog timeout. Listening to `PointerReleased` alone is not sufficient: if the pointer is dragged off the button before release, the release event may not reach the control and the axis keeps moving.
- **The heartbeat indicator is driven by explicit `Beat()` calls**, not by a fixed-period animation. With no incoming events it falls back to a stopped state — a fake heartbeat still ticking after the link has dropped is more dangerous than no heartbeat indicator at all.
- **`Readout` keeps the last known value when data goes stale**, dimming it and labelling the time since the last update. Substituting a placeholder is wrong: the process on the equipment side continues during a comms outage, and the operator needs the last value received before the link dropped.
- **`ParameterRow` writes back the value read from the device** after a successful write, not the value that was typed — devices may clamp or quantise a parameter. A failed write rolls back to the last value that succeeded.
- **A software E-stop does not replace a hardware E-stop circuit.** `EStopButton.HardwareLocationHint` exists to label, in the UI, the physical location of the hardware E-stop device.

</details>

---

## UI Automation

Every control exposes an automation peer. This matters more than usual here: **HMI
acceptance testing is commonly driven through UI Automation**, and a control without
a peer shows up in Inspect as an unnamed `Custom` rectangle that the customer's test
harness cannot address — while the screen itself looks perfectly correct.

Three rules shape the peers:

- **`Value` carries the machine-readable quantity; interpretation goes in `ItemStatus`.**
  A harness reading `"85.4"` cannot tell a live reading from one frozen five minutes ago,
  and on screen the two differ only by a shade of grey. Staleness, deviation and
  write-in-progress are all `ItemStatus`; the unit is `ItemType`, never concatenated into
  `Value`. Enum-backed state reports the enum name, not the display text — display text
  moves with localisation, and an assertion pinned to it breaks on translation.
- **Dangerous actions are not exposed as a single automation call.** E-stop reset requires
  a press-and-hold precisely to prevent accidental release; letting a client undo the latch
  with one `Toggle()` would hand it a shortcut the operator on the floor does not have.
  `EStopButtonAutomationPeer.Toggle()` engages only — it throws `ElementNotEnabledException`
  when already latched. Host veto gates are honoured too: `AlarmBanner`'s `Invoke()` goes
  through `Acknowledge()`, so a host whose command reports `CanExecute == false` still
  cannot be acknowledged from automation.
- **Decorative elements leave the automation tree.** Skeletons, separators and icons add
  noise that buries what a client actually needs to read, so they return a peer that reports
  neither a control nor a content element. Conditional cases follow the same rule: a closed
  `InfoBar` and a `PersonPicture` with no name both drop out.

Elements that appear out of nowhere — alarms, notifications, toasts — declare a live region,
so a client learns about them without polling. `AlarmSeverity.Alarm` and `Fault` are
`Assertive`, `Warning` is `Polite`, `Info` is silent.

---

## High contrast

Two extra theme variants ship with the library: `CobaltFluentTheme.HighContrastLight` and
`CobaltFluentTheme.HighContrastDark`. They are not "the normal theme turned up" — they follow
a different set of rules:

- **Surfaces are flat, hierarchy comes from strokes.** Every background collapses to pure
  black or pure white; separation is carried entirely by borders, which are all full contrast.
- **Nothing is translucent.** A translucent colour's real contrast depends on whatever is
  painted underneath, and a guaranteed contrast ratio is the whole reason these variants
  exist. The only exception is the modal scrim, which has to let the dimmed UI show through.
- **Body text reaches WCAG AAA (7:1).** Disabled text is deliberately kept below that
  threshold — pulling it up too would make "can click" and "cannot click" indistinguishable.
- **Safety colours do not change.** ISO 13850 red and yellow carry regulatory meaning, not
  theming; saturated red cannot reach 7:1 against either white or black text, and turning it
  pink to satisfy a number would remove that meaning. They stay at AA, which is what the
  hue physically allows.
- **The accent is cyan in the dark variant, not the customary yellow**, so that yellow stays
  reserved for the caution state. "This is clickable" and "this needs attention" collapsing
  into one colour costs both meanings.

Selecting a variant is the application's decision — the library never writes
`RequestedThemeVariant` on its own. To follow the operating system's theme and contrast
settings, opt in explicitly:

```csharp
public override void OnFrameworkInitializationCompleted()
{
    CobaltFluentTheme.FollowSystemContrast(this);   // returns IDisposable; dispose to stop
    base.OnFrameworkInitializationCompleted();
}
```

The four variants map onto the two settings the platform reports independently —
`PlatformThemeVariant` (light/dark) and `ColorContrastPreference` (normal/high).
`tools/audit.py` verifies every declared foreground/background pair against its threshold in
all four, and rejects any translucent value in the two high-contrast ones; a missing key
would otherwise inherit the ordinary translucent value in silence.

---

### Tearing tabs out into windows

`TabView` supports drag-reorder, tearing a tab out into its own window, and dragging it back
into any window's tab strip. The torn-out window is itself a `TabView`, so further tabs can be
dropped into it.

**This is a desktop-only capability.** `Avalonia.LinuxFramebuffer` (DRM/KMS, the embedded panel
path), mobile and browser targets are all single-window — their lifetime is
`ISingleViewApplicationLifetime`, which has one `MainView` and no window list. `CanTearOut`
reports whether the current process can actually do it, so the affordance is not offered where
dragging would silently do nothing. Reordering still works everywhere.

Two mechanisms were ruled out and are worth recording:

- **`Window.BeginMoveDrag`** hands the window move to the window manager, after which the
  process receives no pointer events at all — so dragging a torn-out tab *back* cannot be
  detected. This is why Chrome implements its own window dragging on Windows.
- **`DragDrop.DoDragDrop`** is the OS clipboard-based drag protocol: the payload must be
  serialisable, and what is being moved here is a live control instance. Its behaviour also
  varies considerably per platform.

What is used instead is pointer capture: once captured, `PointerMoved` keeps reaching the source
window even after the cursor leaves it. Coordinates are converted with `PointToScreen`, the drag
preview follows via `Window.Position`, and the drop target is resolved by comparing screen
coordinates against each window's tab strip — no OS hit-testing involved. Fully managed, so it
survives trimming and NativeAOT.

The preview window sets `ShowActivated = false`. That one line is the pivot: if the preview
takes activation, the source window loses pointer capture and the drag dies mid-gesture.

The move is split across two dispatcher turns when the source and target live in different
windows. Removing and re-inserting in one turn throws
`Attempt to call InvalidateArrange on wrong LayoutManager` — the invalidation from the removal
is still queued against the old window while the control is already attached to the new
window's layout manager. Reordering inside one window stays synchronous.

Keyboard path: `Ctrl+Shift+PageUp` / `PageDown` moves the focused tab, following the convention
browsers and VS Code already use. Dragging is a pointer-only gesture, and an industrial panel
does not necessarily have a mouse.

## NativeAOT and trimming

The control layer contains no reflection bindings, so a consuming application can publish with
`PublishAot` and `TrimMode=full` without the library contributing a single IL warning. This
matters on embedded targets, where AOT is not optional.

Reflection bindings — `{Binding}` resolved through `ReflectionBindingExtension`, or
`new Binding { Path = "..." }` in C# — look up members by name at runtime. Under full trimming
the target member can be removed, and **the binding then fails silently: the UI still renders,
it just stops updating in that one place.** `AvaloniaUseCompiledBindingsByDefault` is on for the
library, so any regression surfaces as a build warning rather than as a field that quietly
stops refreshing on a machine you cannot attach a debugger to.

CI runs `tools/aot-gate.sh`, which has two halves, because warnings alone are not enough:

1. **Publish** with NativeAOT. Any IL warning originating from this repository fails the build.
   Third-party assemblies that produce warnings are listed in `tools/aot-allow.txt`, each with
   a stated reason; the repository's own code is never listed there.
2. **Run** the resulting native binary. `tools/Cobalt.Fluent.AotProbe` exercises the four theme
   variants, the automation peers and several of the rewritten bindings on the real binary. A
   compiled binding whose path resolves to the wrong member produces no warning at all — only
   running it shows the difference. So does a custom `ThemeVariant` dictionary key that gets
   trimmed, which takes the application down at theme-load time rather than on some later page.

---

## Localization

Text the controls generate themselves goes through `CobaltStrings`, which picks an
implementation from `CultureInfo.CurrentUICulture` — Chinese for `zh*`, English for
everything else. Replace the whole set to use your own wording:

```csharp
CobaltStrings.Current = new CobaltStringsZhHans();   // pin Chinese
CobaltStrings.Current = new MyPlantStrings();        // your plant's terminology
```

There is no resx. `ResourceManager` resolves satellite assemblies by reflection, which
would fail this repository's NativeAOT gate; plain virtual members cost nothing at runtime
and can be replaced wholesale.

Three kinds of text go to three different places, and the boundary matters:

| | Where | Why |
|---|---|---|
| On-screen text, `Name` / `ItemStatus` / `HelpText` | `CobaltStrings` | Read by humans; localized, per the UIA convention |
| `IValueProvider.Value` | **Never localized** | This is what a test harness asserts on. Localize it and every customer's acceptance script turns red the day the UI is translated |
| Exception messages | English literals | Read by developers and test rigs, following the convention for libraries |

Strings exposed as property defaults (`AcknowledgeContent`, column headers) resolve at
construction, so a language change does not rewrite instances that already exist — for an
HMI, language is a deployment-time or shift-change decision, and adding a projection layer
to every property to support hot switching is not worth it. Text a control computes itself
*does* update: those controls subscribe to `CobaltStrings.CurrentChanged` while attached.

---

## NuGet package

```bash
tools/aot-gate.sh      # NativeAOT publish, then run the native binary
tools/pack-gate.sh     # pack, then consume the installed package
```

The package ships XML documentation (the prose is Chinese; signatures and parameter names
are not), a symbol package, and SourceLink metadata, so stepping into the library from a
consuming application works.

`pack-gate.sh` has two halves for the same reason the AOT gate does: **a control library
that builds fine under a project reference and breaks under a package reference is the
classic failure** — the compiled XAML does not make it into the assembly, no template
applies, and nothing inside the repository can see it. So the gate packs, installs into a
throwaway project, and exercises the result. It also runs under invariant globalization,
which is the one place the English default path is tested — the regression suite pins the
language to keep its own results deterministic.

---

## Embedded and touch-panel targets

Configuration options for embedded graphics environments such as Mali GPUs:

| Scenario | Configuration |
|---|---|
| Many run indicators on one screen | `StatusIndicator.IsPulseEnabled="False"` (the static outer ring stays, so the shape encoding survives) |
| Alarm banner breathing | `AlarmBanner.IsBreathingEnabled="False"` (adds the safety-yellow stroke automatically) |
| Skeleton shimmer | `Skeleton.IsShimmerEnabled="False"` |
| Long-running loads | Use the indeterminate `ProgressBar` instead of `ProgressRing` — it drives transform only |
| Acrylic (live blur) | Override `AcrylicBackgroundFillColorDefaultBrush` with a solid colour |
| Larger touch targets | Override `ControlHeight` / `ControlCornerRadius` / `OverlayCornerRadius` |

Long trends need no configuration: `TrendChart` and `Sparkline` cap their vertex count at
the plot's pixel width. An 8-hour shift sampled at 1 Hz is 28,800 points per channel, which
would put 36 vertices in every pixel column of an 800 px plot. Rendering runs min/max
decimation first — two points per column, the minimum and the maximum — so the envelope
matches the full-resolution curve and a three-sample overshoot still shows. Plain
every-Nth downsampling would drop that overshoot, which is the thing the operator opened
the trend to see.

Icons do not depend on Segoe Fluent Icons: all 38 glyphs are vector paths, so rendering is identical across platforms and no font needs to ship with the application. Where the target environment does have the font, `SymbolIcon.UseGlyphFont="True"` switches back to font rendering.

---

## Building and development

```bash
tools/check.sh                        # build the control library (copies to a temp dir to avoid fighting the IDE over obj)
tools/check.sh --gallery              # build the gallery as well
tools/check.sh --only Button.axaml    # merge only the named control-layer file (for parallel work)

python3 tools/audit.py                             # control-layer silent-failure audit (14 checks)
tools/aot-gate.sh                                  # NativeAOT publish, then run the native binary
tools/pack-gate.sh                                 # pack, then consume the installed package
dotnet test tests/Cobalt.Fluent.Tests               # 372 regression tests
dotnet run  --project samples/Cobalt.Fluent.Gallery # run the gallery
```

Four generated artefacts must be committed alongside the source. CI re-runs all four generators and fails the build on any drift:

```bash
python3 tools/gen_tokens.py          # tools/palette.json → Themes/Tokens.axaml
python3 tools/gen_theme_index.py     # control-layer merge list (runs automatically before build)
python3 tools/gen_gallery_pages.py   # gallery table of contents and section skeletons
python3 tools/gen_api_docs.py        # docs/CONTROLS.md
```

Headless screenshot rendering is the visual acceptance check and runs in CI as well; a render failure fails the build:

```bash
dotnet run --project tools/Cobalt.Fluent.Shots -- artifacts/shots Button both
dotnet run --project tools/Cobalt.Fluent.Shots -- artifacts/shots "shell:Readout" dark
```

---

## Documentation

| Document | Contents |
|---|---|
| [`docs/CONVENTIONS.md`](docs/CONVENTIONS.md) | Development conventions — layering, resource-key reference, pseudo-class list, invariants, and a set of pitfalls the compiler cannot catch |
| [`docs/CONTROLS.md`](docs/CONTROLS.md) | Control API reference (77 types, extracted from source by `tools/gen_api_docs.py`) |
| Gallery | 49 sections: visual specification + interactive demo + resource-key reference + source viewer, with a state matrix in 22 of them |

Read `CONVENTIONS.md` before changing a control. Before opening a PR, run the build, the tests and the generators locally — CI runs the same checks, plus a headless render of every section.

---

## Licence

[MIT](LICENSE)

Avalonia (the framework, `Avalonia.Themes.Fluent` and `Avalonia.Controls.DataGrid`) is likewise MIT-licensed, copyright [AvaloniaUI OÜ](https://github.com/AvaloniaUI/Avalonia) and contributors. This library consumes it through `PackageReference` only — no Avalonia source is compiled into or shipped with the package.

The design language follows the Microsoft Windows 11 Fluent Design System; the implementation is written independently and contains no Microsoft code or assets. Icons are independently drawn vector paths, not Segoe Fluent Icons glyph files.
