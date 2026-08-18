using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace Chummer.Core;

public partial class CharacterOptions
{
	private int _intBpAttribute = 10;
	private int _intBpAttributeMax = 15;
	private int _intBpContact = 1;
	private int _intBpMartialArt = 5;
	private int _intBpMartialArtManeuver = 2;
	private int _intBpSkillGroup = 10;
	private int _intBpActiveSkill = 4;
	private int _intBpActiveSkillSpecialization = 2;
	private int _intBpKnowledgeSkill = 2;
	private int _intBpSpell = 3;
	private int _intBpFocus = 1;
	private int _intBpSpirit = 1;
	private int _intBpComplexForm = 1;
	private int _intBpComplexFormOption = 1;

	// Karma variables.
	/// <summary>
	/// BP cost for each Attribute = this value.
	/// </summary>
	public int BpAttribute
	{
		get
		{
			return _intBpAttribute;
		}
		set
		{
			_intBpAttribute = value;
		}
	}

	/// <summary>
	/// BP cost to raise an Attribute to its Metatype Maximum = this value.
	/// </summary>
	public int BpAttributeMax
	{
		get
		{
			return _intBpAttributeMax;
		}
		set
		{
			_intBpAttributeMax = value;
		}
	}

	/// <summary>
	/// BP cost for each Loyalty, Connection, and Group point = this value.
	/// </summary>
	public int BpContact
	{
		get
		{
			return _intBpContact;
		}
		set
		{
			_intBpContact = value;
		}
	}

	/// <summary>
	/// BP cost for each Martial Arts Rating = this value.
	/// </summary>
	public int BpMartialArt
	{
		get
		{
			return _intBpMartialArt;
		}
		set
		{
			_intBpMartialArt = value;
		}
	}

	/// <summary>
	/// BP cost for each Martial Art Maneuver = this value.
	/// </summary>
	public int BpMartialArtManeuver
	{
		get
		{
			return _intBpMartialArtManeuver;
		}
		set
		{
			_intBpMartialArtManeuver = value;
		}
	}

	/// <summary>
	/// BP cost for each Skill Group Rating = this value.
	/// </summary>
	public int BpSkillGroup
	{
		get
		{
			return _intBpSkillGroup;
		}
		set
		{
			_intBpSkillGroup = value;
		}
	}

	/// <summary>
	/// BP cost for each Active Skill Rating = this value.
	/// </summary>
	public int BpActiveSkill
	{
		get
		{
			return _intBpActiveSkill;
		}
		set
		{
			_intBpActiveSkill = value;
		}
	}

	/// <summary>
	/// BP cost for each Active Skill Specialization = this value.
	/// </summary>
	public int BpActiveSkillSpecialization
	{
		get
		{
			return _intBpActiveSkillSpecialization;
		}
		set
		{
			_intBpActiveSkillSpecialization = value;
		}
	}

	/// <summary>
	/// BP cost for each Knowledge Skill Rating = this value.
	/// </summary>
	public int BpKnowledgeSkill
	{
		get
		{
			return _intBpKnowledgeSkill;
		}
		set
		{
			_intBpKnowledgeSkill = value;
		}
	}

	/// <summary>
	/// BP cost for each Spell = this value.
	/// </summary>
	public int BpSpell
	{
		get
		{
			return _intBpSpell;
		}
		set
		{
			_intBpSpell = value;
		}
	}

	/// <summary>
	/// BP cost for each Rating of Foci.
	/// </summary>
	public int BpFocus
	{
		get
		{
			return _intBpFocus;
		}
		set
		{
			_intBpFocus = value;
		}
	}

	/// <summary>
	/// BP cost for each service a Sprit owes = this value.
	/// </summary>
	public int BpSpirit
	{
		get
		{
			return _intBpSpirit;
		}
		set
		{
			_intBpSpirit = value;
		}
	}

	/// <summary>
	/// BP cost for each Complex Form Rating = this value.
	/// </summary>
	public int BpComplexForm
	{
		get
		{
			return _intBpComplexForm;
		}
		set
		{
			_intBpComplexForm = value;
		}
	}

	/// <summary>
	/// BP cost for each Complex Form Option Rating = this value.
	/// </summary>
	public int BpComplexFormOption
	{
		get
		{
			return _intBpComplexFormOption;
		}
		set
		{
			_intBpComplexFormOption = value;
		}
	}

}
