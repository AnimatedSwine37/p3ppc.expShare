﻿using p3ppc.expShare.Template.Configuration;
using System.ComponentModel;

namespace p3ppc.expShare.Configuration;
public class Config : Configurable<Config>
{
    [DisplayName("Inactive Party Exp Multiplier")]
    [Description("Multiplies the amount of exp inactive party members get.\nGenerally you'd want this to be <= 1 like 0.5 for half what they'd get if they were active.\nSet to 0 for no party member exp share.")]
    [DefaultValue(1.0)]
    public double PartyExpMultiplier { get; set; } = 1.0;

    [DisplayName("Inactive Persona Exp Multiplier")]
    [Description("Multiplies the amount of exp inactive Personas get.\nGenerally you'd want this to be <= 1 like 0.5 for half what they'd get if they were active.\nSet to 0 for no Persona exp share.")]
    [DefaultValue(1.0)]
    public double PersonaExpMultiplier { get; set; } = 1.0;

    [DisplayName("Debug Mode")]
    [Description("Logs additional information to the console that is useful for debugging.")]
    [DefaultValue(false)]
    public bool DebugEnabled { get; set; } = false;
}

/// <summary>
/// Allows you to override certain aspects of the configuration creation process (e.g. create multiple configurations).
/// Override elements in <see cref="ConfiguratorMixinBase"/> for finer control.
/// </summary>
public class ConfiguratorMixin : ConfiguratorMixinBase
{
    // 
}