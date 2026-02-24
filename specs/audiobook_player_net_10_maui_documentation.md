# Audiobook Player App
## Technical Documentation

**Platform:** .NET 10 + .NET MAUI  
**Primary Target:** iOS  
**Secondary Target:** Windows (local development/testing)  
**Architecture:** MVVM + Services  
**Document Version:** 1.0

---

# 1. Project Overview

This project describes the architecture and implementation of a lightweight audiobook player similar to BookPlayer.

The application supports:

- Importing MP3 files from local storage (Files / iCloud)
- Grouping files into audiobooks
- Playback with background audio
- Remembering playback position
- Sleep timer (stop after X minutes / end of chapter)
- Chapter navigation
- Playback speed control
- Embedded cover extraction (ID3)
- Optional online cover download

Targeting note:
- iOS is production-first.
- Windows build must remain functional for local dev/testing workflows (import, playback, position persistence, and basic navigation).

---

# 2. Technology Stack

## 2.1 Framework
- .NET 10
- .NET MAUI
- C# 13+

## 2.3 Platform Targets
- iOS: primary runtime and UX baseline.
- Windows: required local dev/test runtime.

## 2.2 NuGet Packages

### Audio Playback
- **Plugin.Maui.Audio**

### MVVM
- **CommunityToolkit.Mvvm**

### ID3 Tag Reading
- **TagLibSharp (taglib)**

### Local Database (optional but recommended)
- **sqlite-net-pcl**

---

# 3. Solution Structure

```
AudiobookApp
 ├── Core
 │    ├── Models
 │    ├── Interfaces
 │    ├── Services
 ├── ViewModels
 ├── Views
 ├── Platforms
 │    └── iOS
 └── Resources
```

---

# 4. Core Functional Modules

## 4.1 Library Management

Responsibilities:
- Import audio files
- Extract metadata
- Create Book + Chapter models
- Persist data

Import via abstraction:
```csharp
IFileImportService.PickFolderOrFilesAsync()
```

iOS implementation notes:
- Use `UIDocumentPickerViewController` for folder selection when available.
- Persist access using security-scoped bookmarks for the selected folder/file URLs.
- Resolve bookmarks on app launch before scanning or playback.
- Fallback to multi-file selection when folder selection is unavailable.

Windows implementation notes:
- Allow folder/file import via MAUI picker abstractions or Windows file/folder pickers.
- Persist local absolute paths (bookmarking not required).
- Keep import service contract identical across platforms.

Book grouping:
- Folder-based grouping (recommended)
- Or manual multi-file selection

---

## 4.2 Audio Playback Service

Implemented via `Plugin.Maui.Audio`.

### Interface

```csharp
public interface IAudioService
{
    Task PlayAsync(string filePath, double startPosition = 0);
    void Pause();
    void Stop();
    void Seek(double positionSeconds);
    double CurrentPosition { get; }
    double Duration { get; }
    bool IsPlaying { get; }
}
```

Responsibilities:
- Load chapter
- Resume from saved position
- Track current time
- Raise PlaybackEnded event

---

## 4.3 Playback Features

### Supported Features
- Play / Pause
- Seek (slider)
- Skip ±15 seconds
- Next / Previous chapter
- Playback speed (0.75x – 2.0x)

### Background Audio (iOS)

Info.plist:
```
UIBackgroundModes → audio
```

---

## 4.4 Remember Playback Position

### MVP Approach
Use Preferences:

```
Preferences.Set($"book_{bookId}_position", seconds);
```

### Recommended Approach
Use SQLite:

Book Table:
- Id
- Title
- LastChapterIndex
- LastPositionSeconds

Auto-save position every 5 seconds.

Optional smart resume:
- Resume at (savedPosition - 10 seconds)

---

## 4.5 Sleep Timer

### Modes
- Stop after X minutes
- Stop at end of chapter
- Stop after N chapters (optional)

Implementation:
- Use `Task.Delay` with CancellationToken
- On trigger → Pause playback

Example:

```csharp
Task.Delay(TimeSpan.FromMinutes(20), token)
    .ContinueWith(t => audioService.Pause());
```

---

## 4.6 Chapter Management

Each Book contains:

- List<Chapter>
- OrderIndex
- Duration
- Bookmark path

Chapter detection:
- Filename sorting
- Track number from ID3 if available

---

## 4.7 Cover Handling

### Step 1 – Embedded Cover
Use TagLib:

```csharp
var file = TagLib.File.Create(filePath);
var picture = file.Tag.Pictures.FirstOrDefault();
```

### Step 2 – Online Fallback
Use OpenLibrary or iTunes Search API.

Cache covers in:
```
FileSystem.AppDataDirectory/covers/
```

---

# 5. UI Architecture

## 5.1 Screens

### LibraryView
- List of books
- Import button
- Progress indicator

### BookDetailView
- Cover
- Chapters list
- Resume button

### PlayerView
- Book title
- Chapter title
- Slider
- Time label
- Controls
- Sleep button

---

# 6. Data Model

## Book
- Guid Id
- string Title
- string Author
- string CoverPath
- int LastChapterIndex
- double LastPositionSeconds

## Chapter
- Guid Id
- Guid BookId
- string Title
- int OrderIndex
- double Duration
- string FilePath
- string? SourceUri
- string? SecurityScopedBookmarkBase64
- double LastPositionSeconds

---

# 7. Background Playback (iOS)

Required:

```
<key>UIBackgroundModes</key>
<array>
    <string>audio</string>
</array>
```

Configure AVAudioSession via platform-specific service if needed.

Windows note:
- Background/lock-screen media integration can be simplified for dev testing; core requirement is stable playback while app window is active.

---

# 8. MVP Development Plan

## Phase 1
- Basic audio playback
- Import MP3
- Remember position

## Phase 2
- Chapter navigation
- Sleep timer
- Background playback

## Phase 3
- Cover extraction
- SQLite persistence
- Smart resume

---

# 9. Future Enhancements

- CarPlay support
- Cloud sync
- Bookmark markers
- Playback statistics
- Equalizer
- Chapter merging for multi-file books

---

# 10. Design Principles

- Offline-first
- Minimalistic UI
- High stability
- Low battery usage
- No account required

---

---

# 11. UX Detailed Flow

This section describes the complete end-to-end user experience flow of the application.

---

## 11.1 First Launch Experience

### Step 1 – Empty Library State
User opens the app for the first time.

Screen displays:
- App logo
- Message: "No audiobooks yet"
- Primary button: "Import Audiobook"

UX Goals:
- Extremely clear primary action
- No clutter
- Educate user briefly that local MP3 files are supported

---

## 11.2 Import Flow

### Option A – Folder Import (Recommended on iOS)
1. User taps "Import Audiobook"
2. Document picker opens
3. User selects a folder containing MP3 files
4. App scans files
5. Metadata extraction runs in background
6. App stores security bookmark for future access
7. Book appears in Library

Feedback:
- Loading indicator while scanning
- If metadata missing → fallback to filename

Edge Cases:
- Unsupported file → show non-blocking warning
- Empty folder → show friendly message

---

## 11.3 Library View Flow

Library screen shows:
- Book cover
- Book title
- Progress indicator (percentage or "Chapter X")

User interactions:
- Tap book → Book Detail
- Long press → Delete / Re-scan metadata

Empty State Behavior:
- Always show Import button

---

## 11.4 Book Detail Flow

Displays:
- Large cover
- Title + author
- Resume button (if progress exists)
- List of chapters

Chapter list behavior:
- Current chapter highlighted
- Completed chapters dimmed
- Tap chapter → Start playback from beginning of chapter

Resume behavior:
- Resume from last position
- Smart resume: -10 seconds rewind

---

## 11.5 Player Screen Flow

### Layout
Top:
- Book title
- Chapter title

Middle:
- Cover image
- Playback progress slider
- Current time / Total duration

Bottom controls:
- Skip -15s
- Play / Pause
- Skip +15s
- Next chapter
- Speed button
- Sleep button

Slider Behavior:
- Drag updates position live
- On release → Seek to new position

Playback Behavior:
- Auto-save position every 5 seconds
- Save position on pause
- Save position on backgrounding app

---

## 11.6 Sleep Timer Flow

User taps Sleep button.

Modal opens with options:
- Off
- 10 min
- 20 min
- 30 min
- Custom time
- End of chapter

When activated:
- Small countdown indicator shown in Player UI
- User can cancel anytime

When timer ends:
- Playback pauses
- Position saved
- Optional subtle vibration

---

## 11.7 Background Playback Flow

When user locks phone:
- Audio continues
- Lock screen shows:
  - Cover
  - Title
  - Chapter
  - Play/Pause
  - Next/Previous

When user reopens app:
- Player reflects correct current state

---

## 11.8 Error Handling UX

Scenarios:

File moved or deleted:
- Show "File unavailable"
- Offer "Re-link file"

Permission expired / bookmark stale:
- Prompt user to re-select folder
- Rebuild bookmark and resume scan/playback

Corrupted file:
- Show non-blocking toast

Playback failure:
- Auto-stop
- Clear error message

---

## 11.9 Deletion Flow

User long-presses book → Delete

Confirmation dialog:
- "Remove from library?"
- Does NOT delete physical file

---

## 11.10 Accessibility UX

- Large tap targets
- Dynamic font support
- VoiceOver labels
- Clear contrast
- Haptic feedback for key actions

---

## 11.11 Performance UX Principles

- Instant player response (<100ms UI reaction)
- No blocking UI during metadata scan
- Lazy loading of covers
- Background thread for heavy operations

---

# End of Document
