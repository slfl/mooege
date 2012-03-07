﻿/*
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mooege.Core.GS.Actors;
using Mooege.Net.GS.Message;
using Mooege.Net.GS.Message.Definitions.Misc;
using Mooege.Net.GS.Message.Definitions.Animation;
using Mooege.Net.GS.Message.Fields;
using Mooege.Core.GS.Players;
using Mooege.Core.GS.Common.Types.TagMap;
using Mooege.Net.GS.Message.Definitions.ACD;
using Mooege.Net.GS.Message.Definitions.Player;
using Mooege.Net.GS.Message.Definitions.Trade;

namespace Mooege.Core.GS.Powers.Payloads
{
    public class DeathPayload : Payload
    {
        public DamageType DeathDamageType;

        public DeathPayload(PowerContext context, DamageType deathDamageType, Actor target)
            : base(context, target)
        {
            this.DeathDamageType = deathDamageType;
        }

        public void Apply()
        {
            if (this.Target.World == null) return;

            // HACK: add to hackish list thats used to defer deleting actor and filter it from powers targetting
            this.Target.World.PowerManager.AddDeletingActor(this.Target);

            // kill brain if monster
            if (this.Target is Monster)
            {
                Monster mon = (Monster)this.Target;
                if (mon.Brain != null)
                    mon.Brain.Kill();
            }

            // send this death payload to buffs
            this.Target.World.BuffManager.SendTargetPayload(this.Target, this);

            // wtf is this?
            this.Target.World.BroadcastIfRevealed(new Mooege.Net.GS.Message.Definitions.Effect.PlayEffectMessage()
            {
                ActorId = this.Target.DynamicID,
                Effect = Mooege.Net.GS.Message.Definitions.Effect.Effect.Unknown12,
            }, this.Target);

            this.Target.World.BroadcastIfRevealed(new ANNDataMessage(Opcodes.ANNDataMessage13)
            {
                ActorID = this.Target.DynamicID
            }, this.Target);

            // play main death animation
            this.Target.PlayAnimation(11, _FindBestDeathAnimationSNO(), 1f, 2);

            this.Target.World.BroadcastIfRevealed(new ANNDataMessage(Opcodes.ANNDataMessage24)
            {
                ActorID = this.Target.DynamicID,
            }, this.Target);

            // remove all buffs and running powers before deleting actor
            this.Target.World.BuffManager.RemoveAllBuffs(this.Target);
            this.Target.World.PowerManager.CancelAllPowers(this.Target);

            this.Target.Attributes[GameAttribute.Deleted_On_Server] = true;
            this.Target.Attributes[GameAttribute.Could_Have_Ragdolled] = true;
            this.Target.Attributes.BroadcastChangedIfRevealed();

            // Spawn Random item and give exp for each player in range
            List<Player> players = this.Target.GetPlayersInRange(26f);
            foreach (Player plr in players)
            {
                plr.UpdateExp(this.Target.Attributes[GameAttribute.Experience_Granted]);
                this.Target.World.SpawnRandomItemDrop(this.Target, plr);
            }

            if (this.Context.User is Player)
            {
                Player player = (Player)this.Context.User;

                player.ExpBonusData.Update(player.GBHandle.Type, this.Target.GBHandle.Type);
                this.Target.World.SpawnGold(this.Target, player);
                if (Mooege.Common.Helpers.Math.RandomHelper.Next(1, 100) < 20)
                    this.Target.World.SpawnHealthGlobe(this.Target, player, this.Target.Position);
            }

            if (this.Target is Monster)
                (this.Target as Monster).PlayLore();

            // HACK: instead of deleting actor right here, its added to a list (near the top of this function)
            //this.Target.Destroy();
        }
    }
}
