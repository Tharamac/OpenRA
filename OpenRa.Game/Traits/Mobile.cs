﻿using System.Collections.Generic;
using System.Linq;

namespace OpenRa.Traits
{
	public class MobileInfo : ITraitInfo
	{
		public readonly UnitMovementType MovementType = UnitMovementType.Wheel;

		public object Create(Actor self) { return new Mobile(self); }
	}

	public class Mobile : IIssueOrder, IResolveOrder, IOccupySpace, IMovement
	{
		readonly Actor self;

		[Sync]
		int2 __fromCell;
		public int2 fromCell
		{
			get { return __fromCell; }
			set { self.World.WorldActor.traits.Get<UnitInfluence>().Remove(self, this); __fromCell = value; self.World.WorldActor.traits.Get<UnitInfluence>().Add(self, this); }
		}
		public int2 toCell
		{
			get { return self.Location; }
			set
			{
				if (self.Location != value)
				{
					self.World.WorldActor.traits.Get<UnitInfluence>().Remove(self, this);
					self.Location = value;
					self.Owner.Shroud.Explore(self);
				}
				self.World.WorldActor.traits.Get<UnitInfluence>().Add(self, this);
			}
		}

		public Mobile(Actor self)
		{
			this.self = self;
			__fromCell = toCell;
			self.World.WorldActor.traits.Get<UnitInfluence>().Add(self, this);
		}

		public void TeleportTo(Actor self, int2 xy)
		{
			fromCell = toCell = xy;
			self.CenterLocation = Util.CenterOfCell(fromCell);
		}

		public Order IssueOrder(Actor self, int2 xy, MouseInput mi, Actor underCursor)
		{
			if (mi.Button == MouseButton.Left) return null;

			// force-fire should *always* take precedence over move.
			if (mi.Modifiers.HasModifier(Modifiers.Ctrl)) return null;
		
			if (underCursor != null && underCursor.Owner != null)
			{
				// force-move
				if (!mi.Modifiers.HasModifier(Modifiers.Alt)) return null;
				if (!self.World.IsActorCrushableByActor(underCursor, self)) return null;
			}

			if (Util.GetEffectiveSpeed(self) == 0) return null;		/* allow disabling move orders from modifiers */
			if (xy == toCell) return null;
			return new Order("Move", self, xy);
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "Move")
			{
				self.CancelActivity();
				self.QueueActivity(new Activities.Move(order.TargetLocation, 8));
			}
		}

		public IEnumerable<int2> OccupiedCells()
		{
			return (fromCell == toCell)
				? new[] { fromCell } 
				: new[] { fromCell, toCell };
		}

		public UnitMovementType GetMovementType()
		{
			return self.Info.Traits.Get<MobileInfo>().MovementType;			
		}
		
		public bool CanEnterCell(int2 a)
		{
			if (!self.World.WorldActor.traits.Get<BuildingInfluence>().CanMoveHere(a)) return false;

			var crushable = true;
			foreach (Actor actor in self.World.WorldActor.traits.Get<UnitInfluence>().GetUnitsAt(a))
			{
				if (actor == self) continue;
				
				if (!self.World.IsActorCrushableByActor(actor, self))
				{
					crushable = false;
					break;
				}
			}
			
			if (!crushable) return false;
			
			return self.World.Map.IsInMap(a.X, a.Y) &&
				TerrainCosts.Cost(GetMovementType(),
					self.World.TileSet.GetWalkability(self.World.Map.MapTiles[a.X, a.Y])) < double.PositiveInfinity;
		}

		public IEnumerable<int2> GetCurrentPath()
		{
			var move = self.GetCurrentActivity() as Activities.Move;
			if (move == null || move.path == null) return new int2[] { };
			return Enumerable.Reverse(move.path);
		}
	}
}
