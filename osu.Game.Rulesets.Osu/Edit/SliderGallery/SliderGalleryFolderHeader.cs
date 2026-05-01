// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Edit.SliderGallery
{
    /// <summary>
    /// A collapsible header for a folder in the slider gallery panel.
    /// Displays the folder name, entry count, and a chevron indicator.
    /// </summary>
    public partial class SliderGalleryFolderHeader : CompositeDrawable, IHasContextMenu, IHasPopover
    {
        public Action<SliderGalleryFolder>? OnRequestDelete;
        public Action<SliderGalleryFolder, string>? OnRequestRename;
        public Action<SliderGalleryFolder>? OnToggleExpanded;

        /// <summary>
        /// Whether this folder is a valid drop target (entry being dragged over it).
        /// </summary>
        public bool IsDropTarget
        {
            set
            {
                if (background == null) return;

                if (value)
                {
                    background.FadeColour(dropTargetColour, 100);
                    this.ScaleTo(1.02f, 150, Easing.OutQuint);
                }
                else
                {
                    background.FadeColour(IsHovered ? hoverColour : idleColour, 200, Easing.OutQuint);
                    this.ScaleTo(1f, 200, Easing.OutQuint);
                }
            }
        }

        private readonly SliderGalleryFolder folder;
        public Guid FolderId => folder.Id;
        private readonly bool expanded;
        private readonly int entryCount;

        private Box background = null!;
        private SpriteIcon chevron = null!;
        private Color4 idleColour;
        private Color4 hoverColour;
        private Color4 dropTargetColour;
        private TruncatingSpriteText folderNameText = null!;

        [Resolved(canBeNull: true)]
        private IExpandingContainer? expandingContainer { get; set; }

        public SliderGalleryFolderHeader(SliderGalleryFolder folder, bool expanded, int entryCount)
        {
            this.folder = folder;
            this.expanded = expanded;
            this.entryCount = entryCount;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, OsuColour colours)
        {
            RelativeSizeAxes = Axes.X;
            Height = 28;
            CornerRadius = 4;
            Masking = true;
            Margin = new MarginPadding { Top = 4 };

            idleColour = colourProvider.Background3;
            hoverColour = colourProvider.Background2;
            dropTargetColour = colourProvider.Highlight1.Opacity(0.5f);

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = idleColour,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Horizontal = 8 },
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(6, 0),
                                Margin = new MarginPadding { Right = 6 },
                                Children = new Drawable[]
                                {
                                    chevron = new SpriteIcon
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Size = new Vector2(10),
                                        Icon = expanded ? FontAwesome.Solid.ChevronDown : FontAwesome.Solid.ChevronRight,
                                        Colour = colourProvider.Light4,
                                    },
                                    new SpriteIcon
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Size = new Vector2(12),
                                        Icon = FontAwesome.Solid.Folder,
                                        Colour = colourProvider.Light3,
                                    },
                                }
                            },
                            folderNameText = new TruncatingSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = folder.Name,
                                Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                                RelativeSizeAxes = Axes.X,
                            },
                            new CircularContainer
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                AutoSizeAxes = Axes.Both,
                                Masking = true,
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = colours.Orange1,
                                    },
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Text = entryCount.ToString(),
                                        Font = OsuFont.GetFont(size: 10, weight: FontWeight.Bold),
                                        Colour = Color4.Black,
                                        Margin = new MarginPadding { Horizontal = 6, Vertical = 1 },
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            expandingContainer?.Expanded.BindValueChanged(containerExpanded =>
            {
                folderNameText.FadeTo(containerExpanded.NewValue ? 1 : 0, 200, Easing.OutQuint);
            }, true);
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
            OnToggleExpanded?.Invoke(folder);
            return true;
        }

        public MenuItem[] ContextMenuItems => new MenuItem[]
        {
            new OsuMenuItem("Rename", MenuItemType.Standard, () => this.ShowPopover()),
            new OsuMenuItem("Delete folder", MenuItemType.Destructive, () => OnRequestDelete?.Invoke(folder)),
        };

        public Popover GetPopover() => new CreateFolderPopover("Rename folder", folder.Name)
        {
            OnCommit = newName => OnRequestRename?.Invoke(folder, newName),
        };
    }
}
