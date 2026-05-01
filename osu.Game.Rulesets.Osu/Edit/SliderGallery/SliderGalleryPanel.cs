// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Cursor;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Screens.Edit;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Edit.SliderGallery
{
    /// <summary>
    /// The main panel that displays all saved sliders in the gallery,
    /// organized by folders with drag/drop support.
    /// </summary>
    public partial class SliderGalleryPanel : CompositeDrawable
    {
        private const float compact_spacing = 4;
        private const int compact_columns = 3;
        private const float content_padding = 6;

        private FillFlowContainer cardContainer = null!;
        private OsuScrollContainer scrollContainer = null!;

        [Resolved]
        private SliderGalleryStorage galleryStorage { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        /// <summary>
        /// Tracks which folders are expanded (by folder ID).
        /// </summary>
        private readonly HashSet<Guid> expandedFolders = new HashSet<Guid>();

        /// <summary>
        /// Whether compact mode is enabled (driven by editor config).
        /// </summary>
        private readonly Bindable<bool> compactMode = new Bindable<bool>();

        /// <summary>
        /// The entry currently being dragged, if any.
        /// </summary>
        internal SliderGalleryEntry? DraggedEntry { get; set; }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, OsuConfigManager config)
        {
            RelativeSizeAxes = Axes.X;
            Height = 300;

            config.BindWith(OsuSetting.EditorGalleryCompactMode, compactMode);

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background5,
                },
                scrollContainer = new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new OsuContextMenuContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Child = cardContainer = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 4),
                            Padding = new MarginPadding { Horizontal = 6, Vertical = 6 },
                        },
                    },
                },
            };

            refreshEntries();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            galleryStorage.EntriesChanged += () => Scheduler.AddOnce(refreshEntries);
            compactMode.BindValueChanged(_ => Scheduler.AddOnce(refreshEntries));
        }

        private void refreshEntries()
        {
            cardContainer.Clear();

            bool isCompact = compactMode.Value;



            // Adjust the flow direction, spacing and padding based on view mode.
            if (isCompact)
            {
                cardContainer.Direction = FillDirection.Full;
                cardContainer.Spacing = Vector2.Zero;
                cardContainer.Padding = new MarginPadding
                {
                    Horizontal = content_padding,
                    Vertical = content_padding,
                };
            }
            else
            {
                cardContainer.Direction = FillDirection.Vertical;
                cardContainer.Spacing = new Vector2(0, 4);
                cardContainer.Padding = new MarginPadding
                {
                    Horizontal = content_padding,
                    Vertical = content_padding,
                };
            }

            var folders = galleryStorage.GetFolders();
            var rootEntries = galleryStorage.GetAll();

            // "Add Folder" button at the top.
            cardContainer.Add(new AddFolderButton
            {
                OnRequestAdd = addFolder,
                Margin = new MarginPadding { Bottom = 2 },
            });

            // Render folders.
            foreach (var folder in folders)
            {
                bool isExpanded = expandedFolders.Contains(folder.Id);
                var entriesInFolder = galleryStorage.GetEntriesInFolder(folder.Id);

                // In compact mode, folder headers still span the full width.
                var header = new SliderGalleryFolderHeader(folder, isExpanded, entriesInFolder.Count)
                {
                    OnToggleExpanded = toggleFolder,
                    OnRequestDelete = requestDeleteFolder,
                    OnRequestRename = requestRenameFolder,
                };

                if (isCompact)
                {
                    // Wrap the header in a full-width container so it breaks the flow.
                    cardContainer.Add(new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Child = header,
                    });
                }
                else
                {
                    cardContainer.Add(header);
                }

                if (isExpanded)
                {
                    foreach (var entry in entriesInFolder)
                    {
                        if (isCompact)
                        {
                            cardContainer.Add(new SliderGalleryEntryCard(entry, compact: true)
                            {
                                OnPlace = placeSlider,
                                OnRequestDelete = requestDeleteEntry,
                                OnRequestRename = requestRenameEntry,
                                OnRequestMoveToFolder = moveEntryToFolder,
                            });
                        }
                        else
                        {
                            cardContainer.Add(new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Padding = new MarginPadding { Left = 16 },
                                Child = new SliderGalleryEntryCard(entry)
                                {
                                    OnPlace = placeSlider,
                                    OnRequestDelete = requestDeleteEntry,
                                    OnRequestRename = requestRenameEntry,
                                    OnRequestMoveToFolder = moveEntryToFolder,
                                },
                            });
                        }
                    }
                }
            }

            // Render ungrouped entries.
            if (rootEntries.Count > 0)
            {
                foreach (var entry in rootEntries)
                {
                    cardContainer.Add(new SliderGalleryEntryCard(entry, compact: isCompact)
                    {
                        OnPlace = placeSlider,
                        OnRequestDelete = requestDeleteEntry,
                        OnRequestRename = requestRenameEntry,
                        OnRequestMoveToFolder = moveEntryToFolder,
                    });
                }
            }

            // Show empty state only when there are no folders and no entries.
            if (folders.Count == 0 && rootEntries.Count == 0)
            {
                cardContainer.Add(new OsuSpriteText
                {
                    Text = "No sliders saved yet.\nRight-click a slider to add one!",
                    Font = OsuFont.GetFont(size: 12),
                    Padding = new MarginPadding(8),
                    Colour = Colour4.Gray,
                });
            }
        }

        private void addFolder(string folderName)
        {
            var folder = galleryStorage.AddFolder(folderName);
            expandedFolders.Add(folder.Id);
        }

        private void toggleFolder(SliderGalleryFolder folder)
        {
            if (!expandedFolders.Remove(folder.Id))
                expandedFolders.Add(folder.Id);

            refreshEntries();
        }

        private void placeSlider(SliderGalleryEntry entry)
        {
            var slider = galleryStorage.CreateSliderFromEntry(entry, editorClock.CurrentTime);

            editorBeatmap.BeginChange();
            editorBeatmap.Add(slider);
            editorBeatmap.SelectedHitObjects.Clear();
            editorBeatmap.SelectedHitObjects.Add(slider);
            editorBeatmap.EndChange();
        }

        private void requestDeleteEntry(SliderGalleryEntry entry)
        {
            if (dialogOverlay != null)
            {
                dialogOverlay.Push(new DeleteSliderGalleryEntryDialog(entry.Name, () =>
                {
                    galleryStorage.Remove(entry.Id);
                }));
            }
            else
            {
                galleryStorage.Remove(entry.Id);
            }
        }

        private void requestRenameEntry(SliderGalleryEntry entry, string newName)
        {
            galleryStorage.Rename(entry.Id, newName);
        }

        private void moveEntryToFolder(SliderGalleryEntry entry, Guid? folderId)
        {
            galleryStorage.MoveToFolder(entry.Id, folderId);
        }

        private void requestDeleteFolder(SliderGalleryFolder folder)
        {
            if (dialogOverlay != null)
            {
                dialogOverlay.Push(new DeleteSliderGalleryEntryDialog($"folder \"{folder.Name}\"", () =>
                {
                    galleryStorage.RemoveFolder(folder.Id);
                }));
            }
            else
            {
                galleryStorage.RemoveFolder(folder.Id);
            }
        }

        private void requestRenameFolder(SliderGalleryFolder folder, string newName)
        {
            galleryStorage.RenameFolder(folder.Id, newName);
        }

        /// <summary>
        /// Handles a drag drop: finds the folder header under the cursor and moves the entry there.
        /// </summary>
        internal void HandleDrop(SliderGalleryEntry entry, DragEndEvent e)
        {
            var screenPos = e.ScreenSpaceMousePosition;

            // Check if we dropped onto a folder header.
            foreach (var child in cardContainer.Children)
            {
                if (child is SliderGalleryFolderHeader header && header.ReceivePositionalInputAt(screenPos))
                {
                    galleryStorage.MoveToFolder(entry.Id, header.FolderId);
                    return;
                }
            }

            // Not dropped on a folder — move to root.
            galleryStorage.MoveToFolder(entry.Id, null);
        }

        /// <summary>
        /// Updates folder header drop target highlighting during a drag.
        /// </summary>
        internal void UpdateDragHighlight(Vector2 screenSpacePosition)
        {
            foreach (var child in cardContainer.Children)
            {
                if (child is SliderGalleryFolderHeader header)
                    header.IsDropTarget = header.ReceivePositionalInputAt(screenSpacePosition);
            }
        }

        /// <summary>
        /// Clears all drop target highlighting.
        /// </summary>
        internal void ClearDragHighlight()
        {
            foreach (var child in cardContainer.Children)
            {
                if (child is SliderGalleryFolderHeader header)
                    header.IsDropTarget = false;
            }
        }

        /// <summary>
        /// A small button that creates a new folder when clicked.
        /// </summary>
        private partial class AddFolderButton : CompositeDrawable, IHasPopover
        {
            public Action<string>? OnRequestAdd;

            private Box background = null!;
            private Color4 idleColour;
            private Color4 hoverColour;

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                RelativeSizeAxes = Axes.X;
                Height = 24;
                CornerRadius = 4;
                Masking = true;

                idleColour = colourProvider.Background4;
                hoverColour = colourProvider.Background3;

                InternalChildren = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = idleColour,
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(4, 0),
                        Children = new Drawable[]
                        {
                            new SpriteIcon
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Size = new Vector2(10),
                                Icon = FontAwesome.Solid.FolderPlus,
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = "Add Folder",
                                Font = OsuFont.GetFont(size: 11, weight: FontWeight.SemiBold),
                            },
                        }
                    }
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                background.FadeColour(hoverColour, 200, Easing.OutQuint);
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                background.FadeColour(idleColour, 200, Easing.OutQuint);
            }

            protected override bool OnClick(ClickEvent e)
            {
                this.ShowPopover();
                return true;
            }

            public Popover GetPopover() => new CreateFolderPopover
            {
                OnCommit = name => OnRequestAdd?.Invoke(name),
            };
        }
    }
}
