# MaterialCombiner

## About
MaterialCombiner is a Unity editor extension that combines multiple materials on objects into a single material.  

## Environment
- Unity 2022.3 or higher

## Installation

```
https://github.com/bigdra50/MaterialCombiner.git?path=Assets/bigdra50/MaterialCombiner
```

## Usage
1. Select "Tools > Material Combiner" from the Unity menu
2. Select objects with materials you want to combine
3. Adjust settings (output path, atlas size, etc.)
4. Click the "Process Selected Objects" button

## How It Works
1. Extracts multiple materials and textures from selected objects
2. Combines these textures into a single texture atlas
3. Recalculates UV coordinates of the original mesh to match positions in the new atlas
4. Creates a new material and applies the combined texture atlas
5. Replaces the object's mesh and materials with the newly created ones
6. Saves processing results (mesh, material, texture) to the specified output path

## License
MIT License
