using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace Chummer.Core;

public partial class CharacterOptions
{
	private bool _blnConfirmKarmaExpense = true;
	private bool _blnFreeKarmaKnowledge = false;
	private bool _blnSpecialKarmaCostBasedOnShownValue = false;
	private bool _blnSpecialAttributeKarmaLimit = false;
	private int _intKarmaAttribute = 5;
	private int _intKarmaQuality = 2;
	private int _intKarmaSpecialization = 2;
	private int _intKarmaNewKnowledgeSkill = 2;
	private int _intKarmaNewActiveSkill = 4;
	private int _intKarmaNewSkillGroup = 10;
	private int _intKarmaImproveKnowledgeSkill = 1;
	private int _intKarmaImproveActiveSkill = 2;
	private int _intKarmaImproveSkillGroup = 5;
	private int _intKarmaSpell = 5;
	private int _intKarmaNewComplexForm = 2;
	private int _intKarmaImproveComplexForm = 1;
	private int _intKarmaComplexFormOption = 2;
	private int _intKarmaComplexFormSkillfot = 1;
	private int _intKarmaNuyenPer = 2500;
	private int _intKarmaContact = 2;
	private int _intKarmaCarryover = 5;
	private int _intKarmaSpirit = 2;
	private int _intKarmaManeuver = 4;
	private int _intKarmaInitiation = 3;
	private int _intKarmaMetamagic = 15;
	private int _intKarmaJoinGroup = 5;
	private int _intKarmaLeaveGroup = 1;

	// Karma Foci variables.
	private int _intKarmaAnchoringFocus = 6;
	private int _intKarmaBanishingFocus = 3;
	private int _intKarmaBindingFocus = 3;
	private int _intKarmaCenteringFocus = 6;
	private int _intKarmaCounterspellingFocus = 3;
	private int _intKarmaDiviningFocus = 6;
	private int _intKarmaDowsingFocus = 6;
	private int _intKarmaInfusionFocus = 3;
	private int _intKarmaMaskingFocus = 6;
	private int _intKarmaPowerFocus = 8;
	private int _intKarmaShieldingFocus = 6;
	private int _intKarmaSpellcastingFocus = 4;
	private int _intKarmaSummoningFocus = 4;
	private int _intKarmaSustainingFocus = 2;
	private int _intKarmaSymbolicLinkFocus = 1;
	private int _intKarmaWeaponFocus = 3;

	// Default build settings.
	/// <summary>
	/// Karma cost to improve an Attribute = New Rating X this value.
	/// </summary>
	public int KarmaAttribute
	{
		get
		{
			return _intKarmaAttribute;
		}
		set
		{
			_intKarmaAttribute = value;
		}
	}

	/// <summary>
	/// Karma cost to purchase a Quality = BP Cost x this value.
	/// </summary>
	public int KarmaQuality
	{
		get
		{
			return _intKarmaQuality;
		}
		set
		{
			_intKarmaQuality = value;
		}
	}

	/// <summary>
	/// Karma cost to purchase a Specialization = this value.
	/// </summary>
	public int KarmaSpecialization
	{
		get
		{
			return _intKarmaSpecialization;
		}
		set
		{
			_intKarmaSpecialization = value;
		}
	}

	/// <summary>
	/// Karma cost to purchase a new Knowledge Skill = this value.
	/// </summary>
	public int KarmaNewKnowledgeSkill
	{
		get
		{
			return _intKarmaNewKnowledgeSkill;
		}
		set
		{
			_intKarmaNewKnowledgeSkill = value;
		}
	}

	/// <summary>
	/// Karma cost to purchase a new Active Skill = this value.
	/// </summary>
	public int KarmaNewActiveSkill
	{
		get
		{
			return _intKarmaNewActiveSkill;
		}
		set
		{
			_intKarmaNewActiveSkill = value;
		}
	}

	/// <summary>
	/// Karma cost to purchase a new Skill Group = this value.
	/// </summary>
	public int KarmaNewSkillGroup
	{
		get
		{
			return _intKarmaNewSkillGroup;
		}
		set
		{
			_intKarmaNewSkillGroup = value;
		}
	}

	/// <summary>
	/// Karma cost to improve a Knowledge Skill = New Rating x this value.
	/// </summary>
	public int KarmaImproveKnowledgeSkill
	{
		get
		{
			return _intKarmaImproveKnowledgeSkill;
		}
		set
		{
			_intKarmaImproveKnowledgeSkill = value;
		}
	}

	/// <summary>
	/// Karma cost to improve an Active Skill = New Rating x this value.
	/// </summary>
	public int KarmaImproveActiveSkill
	{
		get
		{
			return _intKarmaImproveActiveSkill;
		}
		set
		{
			_intKarmaImproveActiveSkill = value;
		}
	}

	/// <summary>
	/// Karma cost to improve a Skill Group = New Rating x this value.
	/// </summary>
	public int KarmaImproveSkillGroup
	{
		get
		{
			return _intKarmaImproveSkillGroup;
		}
		set
		{
			_intKarmaImproveSkillGroup = value;
		}
	}

	/// <summary>
	/// Karma cost for each Spell = this value.
	/// </summary>
	public int KarmaSpell
	{
		get
		{
			return _intKarmaSpell;
		}
		set
		{
			_intKarmaSpell = value;
		}
	}

	/// <summary>
	/// Karma cost for a new Complex Form = this value.
	/// </summary>
	public int KarmaNewComplexForm
	{
		get
		{
			return _intKarmaNewComplexForm;
		}
		set
		{
			_intKarmaNewComplexForm = value;
		}
	}

	/// <summary>
	/// Karma cost to improve a Complex Form = New Rating x this value.
	/// </summary>
	public int KarmaImproveComplexForm
	{
		get
		{
			return _intKarmaImproveComplexForm;
		}
		set
		{
			_intKarmaImproveComplexForm = value;
		}
	}

	/// <summary>
	/// Karma cost for Complex Form Options = Rating x this value.
	/// </summary>
	public int KarmaComplexFormOption
	{
		get
		{
			return _intKarmaComplexFormOption;
		}
		set
		{
			_intKarmaComplexFormOption = value;
		}
	}

	/// <summary>
	/// Karma cost for Complex Form Skillsofts = Rating x this value.
	/// </summary>
	public int KarmaComplexFormSkillsoft
	{
		get
		{
			return _intKarmaComplexFormSkillfot;
		}
		set
		{
			_intKarmaComplexFormSkillfot = value;
		}
	}

	/// <summary>
	/// Amount of Nueyn objtained per Karma point.
	/// </summary>
	public int KarmaNuyenPer
	{
		get
		{
			return _intKarmaNuyenPer;
		}
		set
		{
			_intKarmaNuyenPer = value;
		}
	}

	/// <summary>
	/// Karma cost for a Contact = (Connection + Loyalty) x this value.
	/// </summary>
	public int KarmaContact
	{
		get
		{
			return _intKarmaContact;
		}
		set
		{
			_intKarmaContact = value;
		}
	}

	/// <summary>
	/// Maximum amount of remaining Karma that is carried over to the character once they are created.
	/// </summary>
	public int KarmaCarryover
	{
		get
		{
			return _intKarmaCarryover;
		}
		set
		{
			_intKarmaCarryover = value;
		}
	}

	/// <summary>
	/// Karma cost for a Spirit = this value.
	/// </summary>
	public int KarmaSpirit
	{
		get
		{
			return _intKarmaSpirit;
		}
		set
		{
			_intKarmaSpirit = value;
		}
	}

	/// <summary>
	/// Karma cost for a Combat Maneuver = this value.
	/// </summary>
	public int KarmaManeuver
	{
		get
		{
			return _intKarmaManeuver;
		}
		set
		{
			_intKarmaManeuver = value;
		}
	}

	/// <summary>
	/// Karma cost for a Initiation = 10 + (New Rating x this value).
	/// </summary>
	public int KarmaInitiation
	{
		get
		{
			return _intKarmaInitiation;
		}
		set
		{
			_intKarmaInitiation = value;
		}
	}

	/// <summary>
	/// Karma cost for a Metamagic = this value.
	/// </summary>
	public int KarmaMetamagic
	{
		get
		{
			return _intKarmaMetamagic;
		}
		set
		{
			_intKarmaMetamagic = value;
		}
	}

	/// <summary>
	/// Karma cost to join a Group = this value.
	/// </summary>
	public int KarmaJoinGroup
	{
		get
		{
			return _intKarmaJoinGroup;
		}
		set
		{
			_intKarmaJoinGroup = value;
		}
	}

	/// <summary>
	/// Karma cost to leave a Group = this value.
	/// </summary>
	public int KarmaLeaveGroup
	{
		get
		{
			return _intKarmaLeaveGroup;
		}
		set
		{
			_intKarmaLeaveGroup = value;
		}
	}

	/// <summary>
	/// Karma cost for Anchoring Foci.
	/// </summary>
	public int KarmaAnchoringFocus
	{
		get
		{
			return _intKarmaAnchoringFocus;
		}
		set
		{
			_intKarmaAnchoringFocus = value;
		}
	}

	/// <summary>
	/// Karma cost for Banishing Foci.
	/// </summary>
	public int KarmaBanishingFocus
	{
		get
		{
			return _intKarmaBanishingFocus;
		}
		set
		{
			_intKarmaBanishingFocus = value;
		}
	}

	/// <summary>
	/// Karma cost for Binding Foci.
	/// </summary>
	public int KarmaBindingFocus
	{
		get
		{
			return _intKarmaBindingFocus;
		}
		set
		{
			_intKarmaBindingFocus = value;
		}
	}

	/// <summary>
	/// Karma cost for Centering Foci.
	/// </summary>
	public int KarmaCenteringFocus
	{
		get
		{
			return _intKarmaCenteringFocus;
		}
		set
		{
			_intKarmaCenteringFocus = value;
		}
	}

	/// <summary>
	/// Karma cost for Counterspelling Foci.
	/// </summary>
	public int KarmaCounterspellingFocus
	{
		get
		{
			return _intKarmaCounterspellingFocus;
		}
		set
		{
			_intKarmaCounterspellingFocus = value;
		}
	}

	/// <summary>
	/// Karma cost for Divining Foci.
	/// </summary>
	public int KarmaDiviningFocus
	{
		get
		{
			return _intKarmaDiviningFocus;
		}
		set
		{
			_intKarmaDiviningFocus = value;
		}
	}

	/// <summary>
	/// Karma cost for Dowsing Foci.
	/// </summary>
	public int KarmaDowsingFocus
	{
		get
		{
			return _intKarmaDowsingFocus;
		}
		set
		{
			_intKarmaDowsingFocus = value;
		}
	}

	/// <summary>
	/// Karma cost for Infusion Foci.
	/// </summary>
	public int KarmaInfusionFocus
	{
		get
		{
			return _intKarmaInfusionFocus;
		}
		set
		{
			_intKarmaInfusionFocus = value;
		}
	}

	/// <summary>
	/// Karma cost for Masking Foci.
	/// </summary>
	public int KarmaMaskingFocus
	{
		get
		{
			return _intKarmaMaskingFocus;
		}
		set
		{
			_intKarmaMaskingFocus = value;
		}
	}

	/// <summary>
	/// Karma cost for Power Foci.
	/// </summary>
	public int KarmaPowerFocus
	{
		get
		{
			return _intKarmaPowerFocus;
		}
		set
		{
			_intKarmaPowerFocus = value;
		}
	}

	/// <summary>
	/// Karma cost for Shielding Foci.
	/// </summary>
	public int KarmaShieldingFocus
	{
		get
		{
			return _intKarmaShieldingFocus;
		}
		set
		{
			_intKarmaShieldingFocus = value;
		}
	}

	/// <summary>
	/// Karma cost for Spellcasting Foci.
	/// </summary>
	public int KarmaSpellcastingFocus
	{
		get
		{
			return _intKarmaSpellcastingFocus;
		}
		set
		{
			_intKarmaSpellcastingFocus = value;
		}
	}

	/// <summary>
	/// Karma cost for Summoning Foci.
	/// </summary>
	public int KarmaSummoningFocus
	{
		get
		{
			return _intKarmaSummoningFocus;
		}
		set
		{
			_intKarmaSummoningFocus = value;
		}
	}

	/// <summary>
	/// Karma cost for Sustaining Foci.
	/// </summary>
	public int KarmaSustainingFocus
	{
		get
		{
			return _intKarmaSustainingFocus;
		}
		set
		{
			_intKarmaSustainingFocus = value;
		}
	}

	/// <summary>
	/// Karma cost for Symbolic Link Foci.
	/// </summary>
	public int KarmaSymbolicLinkFocus
	{
		get
		{
			return _intKarmaSymbolicLinkFocus;
		}
		set
		{
			_intKarmaSymbolicLinkFocus = value;
		}
	}

	/// <summary>
	/// Karma cost for Weapon Foci.
	/// </summary>
	public int KarmaWeaponFocus
	{
		get
		{
			return _intKarmaWeaponFocus;
		}
		set
		{
			_intKarmaWeaponFocus = value;
		}
	}

}
