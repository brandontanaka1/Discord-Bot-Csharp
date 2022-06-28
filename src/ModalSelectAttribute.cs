using Discord;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Discord_Bot
{
    public class ModalSelectAttribute : ModalInputAttribute
    {
        public ModalSelectAttribute(string customId, string[] selectableValues) : base(customId)
        {

        }

        public override ComponentType ComponentType => ComponentType.SelectMenu;
    }
}
