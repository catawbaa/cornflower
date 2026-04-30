// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace osu.Game.Rulesets.Osu.Edit.SliderGallery
{
    /// <summary>
    /// Represents a named folder that groups <see cref="SliderGalleryEntry"/> items.
    /// </summary>
    public class SliderGalleryFolder
    {
        [JsonProperty("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("entries")]
        public List<SliderGalleryEntry> Entries { get; set; } = new List<SliderGalleryEntry>();

        [JsonProperty("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
