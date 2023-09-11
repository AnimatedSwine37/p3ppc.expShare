﻿using p3ppc.expShare.Configuration;
using p3ppc.expShare.NuGet.templates.defaultPlus;
using p3ppc.expShare.Template;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.ReloadedII.Interfaces;
using Reloaded.Mod.Interfaces;
using System.IO;
using static p3ppc.expShare.Native;
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;

namespace p3ppc.expShare;
/// <summary>
/// Your mod logic goes here.
/// </summary>
public unsafe class Mod : ModBase // <= Do not Remove.
{
    /// <summary>
    /// Provides access to the mod loader API.
    /// </summary>
    private readonly IModLoader _modLoader;

    /// <summary>
    /// Provides access to the Reloaded.Hooks API.
    /// </summary>
    /// <remarks>This is null if you remove dependency on Reloaded.SharedLib.Hooks in your mod.</remarks>
    private readonly IReloadedHooks? _hooks;

    /// <summary>
    /// Provides access to the Reloaded logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Entry point into the mod, instance that created this class.
    /// </summary>
    private readonly IMod _owner;

    /// <summary>
    /// Provides access to this mod's configuration.
    /// </summary>
    private Config _configuration;

    /// <summary>
    /// The configuration of the currently executing mod.
    /// </summary>
    private readonly IModConfig _modConfig;

    private IHook<SetupResultsExpDelegate> _setupExpHook;
    private IHook<GivePartyMemberExpDelegate> _givePartyMemberExpHook;
    private IHook<LevelUpPartyMemberDelegate> _levelUpPartyMemberHook;

    private GainsInfo?[] _gains = new GainsInfo?[11];

    public Mod(ModContext context)
    {
        _modLoader = context.ModLoader;
        _hooks = context.Hooks;
        _logger = context.Logger;
        _owner = context.Owner;
        _configuration = context.Configuration;
        _modConfig = context.ModConfig;

        Utils.Initialise(_logger, _configuration, _modLoader);
        Native.Initialise(_hooks);

        Utils.SigScan("48 89 54 24 ?? 48 89 4C 24 ?? 53 55 56 57 41 54 41 55 48 83 EC 68", "SetupResultsExp", address =>
        {
            _setupExpHook = _hooks.CreateHook<SetupResultsExpDelegate>(SetupResultsExp, address).Activate();
        });

        Utils.SigScan("48 89 5C 24 ?? 48 89 6C 24 ?? 56 57 41 56 48 83 EC 20 48 89 CF 48 8D 0D ?? ?? ?? ??", "GivePartyMemberExp", address =>
        {
            _givePartyMemberExpHook = _hooks.CreateHook<GivePartyMemberExpDelegate>(GivePartyMemberExp, address).Activate();
        });

        //Utils.SigScan("48 89 5C 24 ?? 48 89 6C 24 ?? 56 57 41 56 48 81 EC B0 00 00 00 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 48 8B E9 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B CD", "LevelUpPartyMember", address =>
        //{
        //    _levelUpPartyMemberHook = _hooks.CreateHook<LevelUpPartyMemberDelegate>(LevelUpPartyMember, address).Activate();
        //});
    }

    private void SetupResultsExp(BattleResults* results, astruct_2* param_2)
    {
        _setupExpHook.OriginalFunction(results, param_2);

        for (PartyMember member = PartyMember.Yukari; member <= PartyMember.Koromaru; member++)
        {
            _gains[(int)member] = null;
            if (!IsInactive(member, results)) continue;

            var persona = GetPartyMemberPersona(member);
            var level = persona->Level;
            if (level >= 99) continue;

            var gainedExp = CalculateGainedExp(level, param_2);
            var currentExp = persona->Exp;
            var requiredExp = GetPersonaRequiredExp(persona, (ushort)(level + 1));
            _gains[(int)member] = new GainsInfo { GainedExp = gainedExp };
            if (requiredExp <= currentExp + gainedExp)
            {
                results->LevelUpStatus |= 0x10; // signify that a party member is ready to level up
                fixed (PersonaStatChanges* statChanges = &_gains[(int)member]!.StatChanges)
                {
                    GenerateLevelUpPersona(persona, statChanges, gainedExp);
                }
            }
        }
    }

    private bool IsInactive(PartyMember member, BattleResults* results)
    {
        for (int i = 0; i < 4; i++)
        {
            if (results->PartyMembers[i] == (short)member) return false;
        }
        // TODO also check for party members actually being available (either by date or a flag in their thing)
        return true;
    }

    private void GivePartyMemberExp(BattleResults* results, nuint param_2, nuint param_3, nuint param_4)
    {
        _givePartyMemberExpHook.OriginalFunction(results, param_2, param_3, param_4);

        for (PartyMember member = PartyMember.Yukari; member <= PartyMember.Koromaru; member++)
        {
            if (!IsInactive(member, results)) continue;

            var persona = GetPartyMemberPersona(member);
            var expGained = _gains[(int)member]!.GainedExp;
            persona->Exp += expGained;
            Utils.LogDebug($"Gave {expGained} exp to {member}");

            if (CanPersonaLevelUp(persona, (nuint)expGained, param_3, param_4) != 0)
            {
                fixed (PersonaStatChanges* statChanges = &_gains[(int)member]!.StatChanges)
                {
                    Utils.LogDebug($"Leveling up {member}");
                    LevelUpPersona(persona, statChanges);
                }
            }
        }
    }

    private void LevelUpPartyMember(BattleResultsThing* results)
    {
        // We only want to change stuff in state 1 (when it's picking a Persona's level up stuff to deal with)
        if (results->Thing->State != 1)
        {
            _levelUpPartyMemberHook.OriginalFunction(results);
            return;
        }



        _levelUpPartyMemberHook.OriginalFunction(results);
    }

    private delegate void SetupResultsExpDelegate(BattleResults* results, astruct_2* param_2);
    private delegate void GivePartyMemberExpDelegate(BattleResults* results, nuint param_2, nuint param_3, nuint param_4);
    private delegate void LevelUpPartyMemberDelegate(BattleResultsThing* results);

    private class GainsInfo
    {
        internal PersonaStatChanges StatChanges;
        internal int GainedExp;
    }

    #region Standard Overrides
    public override void ConfigurationUpdated(Config configuration)
    {
        // Apply settings from configuration.
        // ... your code here.
        _configuration = configuration;
        _logger.WriteLine($"[{_modConfig.ModId}] Config Updated: Applying");
    }
    #endregion

    #region For Exports, Serialization etc.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public Mod() { }
#pragma warning restore CS8618
    #endregion
}