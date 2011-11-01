﻿﻿/*
 * Copyright (C) 2011 mooege project
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 */

using System;
using System.Windows;
using Mooege.Core.GS.Actors;
using Mooege.Core.GS.Common.Types.Math;
using Mooege.Core.GS.Map;
using Mooege.Core.GS.Players;

namespace Mooege.Core.GS.Objects
{
    public abstract class WorldObject : DynamicObject, IRevealable
    {
        public World World { get; protected set; }

        private Vector3D _position;
        public Vector3D Position
        {
            get { return _position; }
            set { 
                _position = value;
                this.Bounds = new Rect(this.Position.X, this.Position.Y, this.Size.Width, this.Size.Height);
                var handler = PositionChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        public event EventHandler PositionChanged;

        public Size Size { get; protected set; }

        public Rect Bounds { get; private set; }

        public float Scale { get; set; }

        public Vector3D RotationAxis { get; set; }

        public float RotationAmount { get; set; }

        protected WorldObject(World world, uint dynamicID)
            : base(dynamicID)
        {
            this.World = world;
            this.World.Game.StartTracking(this);
            this.RotationAxis = new Vector3D();
            this._position = new Vector3D();
        }

        public abstract bool Reveal(Player player);
        public abstract bool Unreveal(Player player);

        public sealed override void Destroy()
        {
            if (this is Actor)
                this.World.Leave(this as Actor);

            this.World.Game.EndTracking(this);
            this.World = null;
        }
    }
}
