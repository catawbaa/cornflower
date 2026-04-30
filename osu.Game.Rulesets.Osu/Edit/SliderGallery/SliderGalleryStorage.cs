// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit.SliderGallery
{
    /// <summary>
    /// Manages persistent storage of slider gallery entries and folders.
    /// Sliders are saved to a JSON file in the osu! data directory.
    /// </summary>
    public class SliderGalleryStorage
    {
        private const string gallery_filename = "slider_gallery.json";

        /// <summary>
        /// Fired when entries or folders are added, removed, renamed, or moved.
        /// </summary>
        public event Action? EntriesChanged;

        private readonly Storage storage;
        private SliderGalleryData data;

        public SliderGalleryStorage(Storage storage)
        {
            this.storage = storage.GetStorageForDirectory("slider-gallery");
            data = loadData();
        }

        #region Entry operations

        /// <summary>
        /// Returns all ungrouped (root-level) gallery entries, ordered by creation date (newest first).
        /// </summary>
        public IReadOnlyList<SliderGalleryEntry> GetAll() => data.Entries.OrderByDescending(e => e.CreatedAt).ToList();

        /// <summary>
        /// Returns all entries in a specific folder, ordered by creation date (newest first).
        /// </summary>
        public IReadOnlyList<SliderGalleryEntry> GetEntriesInFolder(Guid folderId)
        {
            var folder = data.Folders.FirstOrDefault(f => f.Id == folderId);
            return folder?.Entries.OrderByDescending(e => e.CreatedAt).ToList() ?? new List<SliderGalleryEntry>();
        }

        /// <summary>
        /// Saves a slider to the gallery with the given name, optionally in a folder.
        /// </summary>
        public SliderGalleryEntry Add(string name, Slider slider, Guid? folderId = null)
        {
            var entry = new SliderGalleryEntry
            {
                Name = name,
                ControlPoints = slider.Path.ControlPoints.Select(cp => new SerializablePathControlPoint(cp)).ToList(),
                ExpectedDistance = slider.Path.ExpectedDistance.Value,
                SliderVelocityMultiplier = slider.SliderVelocityMultiplier,
                RepeatCount = slider.RepeatCount,
            };

            if (folderId.HasValue)
            {
                var folder = data.Folders.FirstOrDefault(f => f.Id == folderId.Value);

                if (folder != null)
                {
                    entry.FolderId = folderId;
                    folder.Entries.Add(entry);
                }
                else
                {
                    // Folder not found, add to root.
                    data.Entries.Add(entry);
                }
            }
            else
            {
                data.Entries.Add(entry);
            }

            saveData();
            EntriesChanged?.Invoke();
            return entry;
        }

        /// <summary>
        /// Removes a gallery entry by its ID, searching both root and all folders.
        /// </summary>
        public bool Remove(Guid id)
        {
            int removed = data.Entries.RemoveAll(e => e.Id == id);

            if (removed == 0)
            {
                foreach (var folder in data.Folders)
                {
                    removed = folder.Entries.RemoveAll(e => e.Id == id);

                    if (removed > 0)
                        break;
                }
            }

            if (removed > 0)
            {
                saveData();
                EntriesChanged?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Renames a gallery entry.
        /// </summary>
        public bool Rename(Guid id, string newName)
        {
            var entry = findEntry(id);

            if (entry == null)
                return false;

            entry.Name = newName;
            saveData();
            EntriesChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Moves an entry to a folder, or to root (ungrouped) if <paramref name="targetFolderId"/> is null.
        /// </summary>
        public bool MoveToFolder(Guid entryId, Guid? targetFolderId)
        {
            // Find and remove the entry from wherever it currently is.
            SliderGalleryEntry? entry = null;

            int idx = data.Entries.FindIndex(e => e.Id == entryId);

            if (idx >= 0)
            {
                entry = data.Entries[idx];
                data.Entries.RemoveAt(idx);
            }
            else
            {
                foreach (var folder in data.Folders)
                {
                    idx = folder.Entries.FindIndex(e => e.Id == entryId);

                    if (idx >= 0)
                    {
                        entry = folder.Entries[idx];
                        folder.Entries.RemoveAt(idx);
                        break;
                    }
                }
            }

            if (entry == null)
                return false;

            // Place the entry in its new location.
            if (targetFolderId.HasValue)
            {
                var targetFolder = data.Folders.FirstOrDefault(f => f.Id == targetFolderId.Value);

                if (targetFolder != null)
                {
                    entry.FolderId = targetFolderId;
                    targetFolder.Entries.Add(entry);
                }
                else
                {
                    // Target folder not found, fall back to root.
                    entry.FolderId = null;
                    data.Entries.Add(entry);
                }
            }
            else
            {
                entry.FolderId = null;
                data.Entries.Add(entry);
            }

            saveData();
            EntriesChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Creates a new <see cref="Slider"/> from a gallery entry, centered in the playfield.
        /// </summary>
        public Slider CreateSliderFromEntry(SliderGalleryEntry entry, double startTime)
        {
            var controlPoints = entry.ControlPoints.Select(cp => cp.ToPathControlPoint()).ToList();

            // Calculate the bounding box of the control points to center the slider.
            var path = new SliderPath(controlPoints.ToArray(), entry.ExpectedDistance);

            // Sample the path to find the visual bounding box.
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            for (int i = 0; i <= 100; i++)
            {
                float t = i / 100f;
                var pos = path.PositionAt(t);

                minX = Math.Min(minX, pos.X);
                minY = Math.Min(minY, pos.Y);
                maxX = Math.Max(maxX, pos.X);
                maxY = Math.Max(maxY, pos.Y);
            }

            // Also include control point at origin (0,0 is the slider head position).
            minX = Math.Min(minX, 0);
            minY = Math.Min(minY, 0);
            maxX = Math.Max(maxX, 0);
            maxY = Math.Max(maxY, 0);

            var center = new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);
            var playfieldCenter = OsuPlayfield.BASE_SIZE / 2;

            // The slider's Position is the head position.
            // We want the visual center of the slider to be at the playfield center.
            var sliderPosition = playfieldCenter - center;

            var slider = new Slider
            {
                StartTime = startTime,
                Position = sliderPosition,
                RepeatCount = entry.RepeatCount,
                SliderVelocityMultiplier = entry.SliderVelocityMultiplier,
            };

            slider.Path.ControlPoints.AddRange(controlPoints);

            if (entry.ExpectedDistance.HasValue)
                slider.Path.ExpectedDistance.Value = entry.ExpectedDistance.Value;

            return slider;
        }

        #endregion

        #region Folder operations

        /// <summary>
        /// Returns all folders, ordered by creation date (newest first).
        /// </summary>
        public IReadOnlyList<SliderGalleryFolder> GetFolders() => data.Folders.OrderByDescending(f => f.CreatedAt).ToList();

        /// <summary>
        /// Creates a new empty folder with the given name.
        /// </summary>
        public SliderGalleryFolder AddFolder(string name)
        {
            var folder = new SliderGalleryFolder { Name = name };
            data.Folders.Add(folder);
            saveData();
            EntriesChanged?.Invoke();
            return folder;
        }

        /// <summary>
        /// Removes a folder by its ID. Entries in the folder are moved to ungrouped.
        /// </summary>
        public bool RemoveFolder(Guid folderId)
        {
            var folder = data.Folders.FirstOrDefault(f => f.Id == folderId);

            if (folder == null)
                return false;

            // Move all entries from the folder to root.
            foreach (var entry in folder.Entries)
                entry.FolderId = null;

            data.Entries.AddRange(folder.Entries);
            data.Folders.Remove(folder);

            saveData();
            EntriesChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Renames a folder.
        /// </summary>
        public bool RenameFolder(Guid folderId, string newName)
        {
            var folder = data.Folders.FirstOrDefault(f => f.Id == folderId);

            if (folder == null)
                return false;

            folder.Name = newName;
            saveData();
            EntriesChanged?.Invoke();
            return true;
        }

        #endregion

        #region Persistence

        private SliderGalleryEntry? findEntry(Guid id)
        {
            var entry = data.Entries.FirstOrDefault(e => e.Id == id);

            if (entry != null)
                return entry;

            foreach (var folder in data.Folders)
            {
                entry = folder.Entries.FirstOrDefault(e => e.Id == id);

                if (entry != null)
                    return entry;
            }

            return null;
        }

        private SliderGalleryData loadData()
        {
            try
            {
                using var stream = storage.GetStream(gallery_filename, FileAccess.Read, FileMode.OpenOrCreate);

                if (stream == null || stream.Length == 0)
                    return new SliderGalleryData();

                using var reader = new StreamReader(stream);
                string json = reader.ReadToEnd();

                // Try to detect the format: if the root is an array, it's the old flat format.
                var token = JToken.Parse(json);

                if (token.Type == JTokenType.Array)
                {
                    // Migrate from old flat array format.
                    var entries = token.ToObject<List<SliderGalleryEntry>>() ?? new List<SliderGalleryEntry>();

                    Logger.Log("Migrating slider gallery from flat array to versioned format.", LoggingTarget.Runtime, LogLevel.Important);

                    var migrated = new SliderGalleryData
                    {
                        Version = 1,
                        Entries = entries,
                    };

                    return migrated;
                }

                return JsonConvert.DeserializeObject<SliderGalleryData>(json) ?? new SliderGalleryData();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to load slider gallery");
                return new SliderGalleryData();
            }
        }

        private void saveData()
        {
            try
            {
                using var stream = storage.CreateFileSafely(gallery_filename);
                using var writer = new StreamWriter(stream);
                writer.Write(JsonConvert.SerializeObject(data, Formatting.Indented));
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to save slider gallery");
            }
        }

        #endregion
    }
}
