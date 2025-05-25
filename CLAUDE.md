# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview
MaterialCombiner is a Unity editor extension (Unity 2022.3+) that combines multiple materials on objects into a single material by creating texture atlases and recalculating UV coordinates.

## Development Commands
- **Install Package**: Add via Unity Package Manager: `https://github.com/bigdra50/MaterialCombiner.git?path=Assets/bigdra50/MaterialCombiner`
- **Open Tool**: Unity menu `Tools > Material Combiner`
- **Run Tests**: Unity Test Runner window (`Window > General > Test Runner`)

## Architecture
The codebase follows Unity package structure with editor-only code:

### Core Components
- **MaterialCombiner.cs**: Main orchestrator handling multi-object processing with progress tracking
- **MaterialCombinerWindow.cs**: UI using Unity's UI Elements (UXML/USS) instead of IMGUI
- **TextureProcessing.cs**: Texture atlas creation with proper color space handling (Linear/sRGB)
- **MeshProcessing.cs**: UV recalculation and mesh creation with material property copying
- **MaterialCombinerConfig.cs**: Configuration record with sensible defaults

### Key Processing Flow
1. Extract materials/textures from selected objects
2. Create texture atlas with configurable size/padding
3. Recalculate UV coordinates to match atlas layout
4. Copy material properties (shader properties, keywords, render queue)
5. Create new mesh/material and save to specified path

### Unity-Specific Patterns
- All code wrapped in `#if UNITY_EDITOR`
- Separate assembly definitions for Editor and Tests
- Modern UI Elements approach (UXML/USS files in UI folder)
- Proper AssetDatabase integration with refresh calls
- NUnit testing with UnityTest coroutines for integration tests

### Testing
Tests use Unity Test Framework with proper setup/teardown and asset cleanup. Test files are isolated in Tests/Editor/ with separate assembly definition.