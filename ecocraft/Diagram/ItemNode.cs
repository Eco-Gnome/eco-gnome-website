﻿using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using ecocraft.Models;

namespace ecocraft.Diagram;

public class ItemNode(ItemOrTag itemOrTag, Point? position = null) : EcoNode(position)
{
    public ItemOrTag ItemOrTag { get; set; } = itemOrTag;
}