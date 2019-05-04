﻿using System;

public enum BlockTypes : byte
{
	Dirt, Stone, Diamond, Bedrock, Redstone, Sand, Leaves, Wood, Woodbase,
	Water,
	Grass, // types that have different textures on sides and bottom
	Air
}

[Flags]
public enum Cubesides : byte { Right = 1, Left = 2, Top = 4, Bottom = 8, Front = 16, Back = 32 }

public enum WorldGeneratorStatus { NotReady, TerrainReady, FacesReady, AllReady }

public enum ChunkStatus { NotReady, NeedToBeRedrawn, NeedToBeRecreated, Ready }

public enum TreeProbability { None, Some, Lots }