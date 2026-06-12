# Git Integration Scroll View Layout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wrap the main contents of the Git Integration window in a ScrollView to prevent layout overflow on smaller screen heights, while keeping the header and help box fixed at the top.

**Architecture:** A vertical `ScrollView` is added directly to the window's root layout below the help box. The two state-switching layout containers (`mainContentContainer` and `noRepoContainer`) are added inside this ScrollView instead of the window's root.

**Tech Stack:** Unity 6 UI Toolkit (C#, VisualElements, ScrollView)

---

### Task 1: Update GUI Hierarchy in GitIntegrationWindow

**Files:**
- Modify: [GitIntegrationWindow.cs](file:///p:/Personal/00%20Unity/03%20RexTools/RexTools/RexTools-Unity6/Editor/Git%20Integration/GitIntegrationWindow.cs)

- [x] **Step 1: Modify CreateGUI to introduce a vertical ScrollView**

Modify `CreateGUI` to instantiate a vertical `ScrollView` and place the container switchers inside it.

Replace lines 98-106:
```csharp
            // --- CONTAINER SWITCHERS ---
            mainContentContainer = new VisualElement();
            noRepoContainer = new VisualElement();

            root.Add(mainContentContainer);
            root.Add(noRepoContainer);
```

With:
```csharp
            // --- SCROLLABLE CONTENT AREA ---
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            scrollView.style.marginTop = 4;
            scrollView.style.marginBottom = 4;
            root.Add(scrollView);

            // --- CONTAINER SWITCHERS ---
            mainContentContainer = new VisualElement();
            noRepoContainer = new VisualElement();

            scrollView.Add(mainContentContainer);
            scrollView.Add(noRepoContainer);
```

- [x] **Step 2: Verify code compiles and layout functions in Unity**
Open Unity, check the Console for any compilation errors. Open the Git Integration window via `Tools/Rex Tools/Git Integration`. Verify that:
1. The header and help box (if toggled) remain visible at the top.
2. The repository info, changed files list foldout, and operations panel scroll correctly when the window's height is reduced.
3. The "No repository found" warning and scan button scroll correctly when git repository is not detected.
