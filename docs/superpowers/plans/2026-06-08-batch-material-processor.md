# Batch Material Processor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a standalone Unity editor tool called "Batch Material Processor" to change materials to a target shader and automatically assign matching textures from a folder using a flexible suffix/prefix-matching algorithm.

**Architecture:** A standalone window in `Editor/Batch Material Processor/` using UI Toolkit, with settings saved in a persistent `ScriptableObject` that integrates with `RexPresetManager`.

**Tech Stack:** Unity 6, UI Toolkit (UXML/USS), C#, UnityEditor APIs.

---

## Proposed Changes

### Component: Editor/Batch Material Processor

#### [NEW] [BatchMaterialProcessorTypes.cs](file:///p:/Personal/00%20Unity/03%20RexTools/RexTools/RexTools-Unity6/Editor/Batch%20Material%20Processor/BatchMaterialProcessorTypes.cs)
Holds the serialization data structures for mapping suffixes, texture properties, and match results.

#### [NEW] [BatchMaterialProcessorSettings.cs](file:///p:/Personal/00%20Unity/03%20RexTools/RexTools/RexTools-Unity6/Editor/Batch%20Material%20Processor/BatchMaterialProcessorSettings.cs)
A ScriptableObject storing user settings, list of materials, suffix mappings, and dry-run preview results.

#### [NEW] [BatchMaterialProcessorWindow.uxml](file:///p:/Personal/00%20Unity/03%20RexTools/RexTools/RexTools-Unity6/Editor/Batch%20Material%20Processor/BatchMaterialProcessorWindow.uxml)
UXML layout of the tool using a two-column, responsive structure with tab switches.

#### [NEW] [BatchMaterialProcessorWindow.cs](file:///p:/Personal/00%20Unity/03%20RexTools/RexTools/RexTools-Unity6/Editor/Batch%20Material%20Processor/BatchMaterialProcessorWindow.cs)
Main window logic handling drag-and-drop, folder path selection, matching algorithm, preset saving, and applying textures to materials.

---

## Tasks

### Task 1: Create Data Types
Create the serialization and data models for property matches, suffix maps, and preview results.

**Files:**
- Create: `Editor/Batch Material Processor/BatchMaterialProcessorTypes.cs`

- [ ] **Step 1: Write types code**
  Write code in `Editor/Batch Material Processor/BatchMaterialProcessorTypes.cs`:
  ```csharp
  using System.Collections.Generic;
  using UnityEngine;

  namespace RexTools.BatchMaterialProcessor.Editor
  {
      [System.Serializable]
      public class SuffixMapping
      {
          public string propertyName;
          public string propertyDescription;
          public string suffixes; // Comma-separated
      }

      [System.Serializable]
      public class PropertyMatchEntry
      {
          public string propertyName;
          public string propertyDescription;
          public Texture matchedTexture;
          public Texture overrideTexture;
          public bool isSelected = true;
      }

      [System.Serializable]
      public class MaterialMatchResult
      {
          public Material material;
          public List<PropertyMatchEntry> propertyMatches = new List<PropertyMatchEntry>();
          public bool isExpanded = true;
      }
  }
  ```

- [ ] **Step 2: Verify compilation**
  Wait for Unity compile.

---

### Task 2: Create Settings ScriptableObject
Create the persistent settings ScriptableObject so mappings, search folders, and results survive recompilation.

**Files:**
- Create: `Editor/Batch Material Processor/BatchMaterialProcessorSettings.cs`

- [ ] **Step 1: Write settings class**
  Write code in `Editor/Batch Material Processor/BatchMaterialProcessorSettings.cs`:
  ```csharp
  using System.Collections.Generic;
  using UnityEngine;

  namespace RexTools.BatchMaterialProcessor.Editor
  {
      public class BatchMaterialProcessorSettings : ScriptableObject
      {
          public List<Material> materials = new List<Material>();
          public Shader targetShader;
          public string searchFolderPath = "Assets";
          public bool recursiveSearch = false;
          public List<SuffixMapping> suffixMappings = new List<SuffixMapping>();
          public List<MaterialMatchResult> matchResults = new List<MaterialMatchResult>();
      }
  }
  ```

---

### Task 3: Create UI Layout (UXML)
Define the responsive two-column UI Toolkit interface, applying standard RexTools styling classes.

**Files:**
- Create: `Editor/Batch Material Processor/BatchMaterialProcessorWindow.uxml`

- [ ] **Step 1: Write UXML content**
  Write UXML layout code into the file.

---

### Task 4: Implement Main Window Logic
Write the logic connecting the settings persistence, file drag-and-drop events, browse dialogs, the texture prefix-suffix matching algorithm, and the final material updates.

**Files:**
- Create: `Editor/Batch Material Processor/BatchMaterialProcessorWindow.cs`

- [ ] **Step 1: Write EditorWindow implementation**
  Write the detailed window implementation code.

---

## Verification Plan

### Manual Verification
1. Open the tool via `Tools > Rex Tools > Batch Material Processor`.
2. Add materials, set URP Lit shader, set folder path.
3. Verify matching results preview.
4. Click Apply and check the output.
