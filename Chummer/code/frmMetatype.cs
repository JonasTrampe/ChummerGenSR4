﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Xml;
using System.Xml.XPath;

namespace Chummer
{
    public partial class frmMetatype : Form
    {
		private readonly Character _objCharacter;

		private string _strXmlFile = "metatypes.xml";

		private readonly List<ListItem> _lstCategory = new List<ListItem>();

		#region Character Events
		private void objCharacter_MAGEnabledChanged(object sender)
		{
			// Do nothing. This is just an Event trap so an exception doesn't get thrown.
		}

		private void objCharacter_RESEnabledChanged(object sender)
		{
			// Do nothing. This is just an Event trap so an exception doesn't get thrown.
		}

		private void objCharacter_AdeptTabEnabledChanged(object sender)
		{
			// Do nothing. This is just an Event trap so an exception doesn't get thrown.
		}

		private void objCharacter_MagicianTabEnabledChanged(object sender)
		{
			// Do nothing. This is just an Event trap so an exception doesn't get thrown.
		}

		private void objCharacter_TechnomancerTabEnabledChanged(object sender)
		{
			// Do nothing. This is just an Event trap so an exception doesn't get thrown.
		}

		private void objCharacter_InitiationTabEnabledChanged(object sender)
		{
			// Do nothing. This is just an Event trap so an exception doesn't get thrown.
		}

		private void objCharacter_CritterTabEnabledChanged(object sender)
		{
			// Do nothing. This is just an Event trap so an exception doesn't get thrown.
		}
		#endregion

		#region Properties
		/// <summary>
		/// XML file to read Metatype/Critter information from.
		/// </summary>
		public string XmlFile
		{
			set
			{
				_strXmlFile = value;
			}
		}
		#endregion

		#region Form Events
		public frmMetatype(Character objCharacter)
        {
			_objCharacter = objCharacter;
            InitializeComponent();
			LanguageManager.Instance.Load(GlobalOptions.Instance.Language, this);

			// Attach EventHandlers for MAGEnabledChange and RESEnabledChanged since some Metatypes can enable these.
			_objCharacter.MAGEnabledChanged += objCharacter_MAGEnabledChanged;
			_objCharacter.RESEnabledChanged += objCharacter_RESEnabledChanged;
			_objCharacter.AdeptTabEnabledChanged += objCharacter_AdeptTabEnabledChanged;
			_objCharacter.MagicianTabEnabledChanged += objCharacter_MagicianTabEnabledChanged;
			_objCharacter.TechnomancerTabEnabledChanged += objCharacter_TechnomancerTabEnabledChanged;
			_objCharacter.InitiationTabEnabledChanged += objCharacter_InitiationTabEnabledChanged;
			_objCharacter.CritterTabEnabledChanged += objCharacter_CritterTabEnabledChanged;
        }

		private void frmMetatype_FormClosed(object sender, FormClosedEventArgs e)
		{
			// Detach EventHandlers for MAGEnabledChange and RESEnabledChanged since some Metatypes can enable these.
			_objCharacter.MAGEnabledChanged -= objCharacter_MAGEnabledChanged;
			_objCharacter.RESEnabledChanged -= objCharacter_RESEnabledChanged;
			_objCharacter.AdeptTabEnabledChanged -= objCharacter_AdeptTabEnabledChanged;
			_objCharacter.MagicianTabEnabledChanged -= objCharacter_MagicianTabEnabledChanged;
			_objCharacter.TechnomancerTabEnabledChanged -= objCharacter_TechnomancerTabEnabledChanged;
			_objCharacter.InitiationTabEnabledChanged -= objCharacter_InitiationTabEnabledChanged;
			_objCharacter.CritterTabEnabledChanged -= objCharacter_CritterTabEnabledChanged;
		}

        private void frmMetatype_Load(object sender, EventArgs e)
        {
			// Load the Metatype information.
			XmlDocument objXmlDocument = XmlManager.Instance.Load(_strXmlFile);

        	// Populate the Metatype Category list.
			XmlNodeList objXmlCategoryList = objXmlDocument.SelectNodes("/chummer/categories/category");

			// Create a list of any Categories that should not be in the list.
			List<string> lstRemoveCategory = new List<string>();
			foreach (XmlNode objXmlCategory in objXmlCategoryList)
			{
				bool blnRemoveItem = true;

				string strXPath = "/chummer/metatypes/metatype[category = \"" + objXmlCategory.InnerText + "\" and (" + _objCharacter.Options.BookXPath() + ")]";

				XmlNodeList objItems = objXmlDocument.SelectNodes(strXPath);
				if (objItems.Count > 0)
					blnRemoveItem = false;

				if (blnRemoveItem)
					lstRemoveCategory.Add(objXmlCategory.InnerText);
			}

			foreach (XmlNode objXmlCategory in objXmlCategoryList)
			{
				// Make sure the Category isn't in the exclusion list.
				bool blnAddItem = true;
				foreach (string strCategory in lstRemoveCategory)
				{
					if (strCategory == objXmlCategory.InnerText)
						blnAddItem = false;
				}
				// Also make sure it is not already in the Category list.
				foreach (ListItem objItem in _lstCategory)
				{
					if (objItem.Value == objXmlCategory.InnerText)
						blnAddItem = false;
				}

				if (blnAddItem)
				{
					ListItem objItem = new ListItem();
					objItem.Value = objXmlCategory.InnerText;
					if (objXmlCategory.Attributes != null)
					{
						if (objXmlCategory.Attributes["translate"] != null)
							objItem.Name = objXmlCategory.Attributes["translate"].InnerText;
						else
							objItem.Name = objXmlCategory.InnerText;
					}
					else
						objItem.Name = objXmlCategory.InnerXml;
					_lstCategory.Add(objItem);
				}
			}

			SortListItem objSort = new SortListItem();
			_lstCategory.Sort(objSort.Compare);
			cboCategory.ValueMember = "Value";
			cboCategory.DisplayMember = "Name";
			cboCategory.DataSource = _lstCategory;

			// Attempt to select the default Metahuman Category. If it could not be found, select the first item in the list instead.
			try
			{
				cboCategory.SelectedValue = "Metahuman";
			}
			catch
			{
				cboCategory.SelectedIndex = 0;
			}
			if (cboCategory.SelectedIndex == -1)
				cboCategory.SelectedIndex = 0;

			this.Height = cmdOK.Bottom + 40;
			lstMetatypes.Height = cmdOK.Bottom - lstMetatypes.Top;

			// Add Possession and Inhabitation to the list of Critter Tradition variations.
			tipTooltip.SetToolTip(chkPossessionBased, LanguageManager.Instance.GetString("Tip_Metatype_PossessionTradition"));
			tipTooltip.SetToolTip(chkBloodSpirit, LanguageManager.Instance.GetString("Tip_Metatype_BloodSpirit"));

			objXmlDocument = XmlManager.Instance.Load("critterpowers.xml");
			XmlNode objXmlPossession = objXmlDocument.SelectSingleNode("/chummer/powers/power[name = \"Possession\"]");
			XmlNode objXmlInhabitation = objXmlDocument.SelectSingleNode("/chummer/powers/power[name = \"Inhabitation\"]");
			List<ListItem> lstMethods = new List<ListItem>();
			
			ListItem objPossession = new ListItem();
			objPossession.Value = "Possession";
			if (objXmlPossession["translate"] != null)
				objPossession.Name = objXmlPossession["translate"].InnerText;
			else
				objPossession.Name = objXmlPossession["name"].InnerText;

			ListItem objInhabitation = new ListItem();
			objInhabitation.Value = "Inhabitation";
			if (objXmlInhabitation["translate"] != null)
				objInhabitation.Name = objXmlInhabitation["translate"].InnerText;
			else
				objInhabitation.Name = objXmlInhabitation["name"].InnerText;

			lstMethods.Add(objInhabitation);
			lstMethods.Add(objPossession);

			SortListItem objSortPossession = new SortListItem();
			lstMethods.Sort(objSortPossession.Compare);
			cboPossessionMethod.ValueMember = "Value";
			cboPossessionMethod.DisplayMember = "Name";
			cboPossessionMethod.DataSource = lstMethods;
			cboPossessionMethod.SelectedIndex = cboPossessionMethod.FindStringExact(objPossession.Name);
        }
		#endregion

		#region Control Events
		private void lstMetatypes_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Don't attempt to do anything if nothing is selected.
			if (lstMetatypes.Text != "")
			{
				XmlDocument objXmlDocument = XmlManager.Instance.Load(_strXmlFile);

				XmlNode objXmlMetatype = objXmlDocument.SelectSingleNode("/chummer/metatypes/metatype[name = \"" + lstMetatypes.SelectedValue + "\"]");

				lblBP.Text = objXmlMetatype["bp"].InnerText;
				if (objXmlMetatype["forcecreature"] == null)
				{
					lblBOD.Text = string.Format("{0}/{1} ({2})", objXmlMetatype["bodmin"].InnerText, objXmlMetatype["bodmax"].InnerText, objXmlMetatype["bodaug"].InnerText);
					lblAGI.Text = string.Format("{0}/{1} ({2})", objXmlMetatype["agimin"].InnerText, objXmlMetatype["agimax"].InnerText, objXmlMetatype["agiaug"].InnerText);
					lblREA.Text = string.Format("{0}/{1} ({2})", objXmlMetatype["reamin"].InnerText, objXmlMetatype["reamax"].InnerText, objXmlMetatype["reaaug"].InnerText);
					lblSTR.Text = string.Format("{0}/{1} ({2})", objXmlMetatype["strmin"].InnerText, objXmlMetatype["strmax"].InnerText, objXmlMetatype["straug"].InnerText);
					lblCHA.Text = string.Format("{0}/{1} ({2})", objXmlMetatype["chamin"].InnerText, objXmlMetatype["chamax"].InnerText, objXmlMetatype["chaaug"].InnerText);
					lblINT.Text = string.Format("{0}/{1} ({2})", objXmlMetatype["intmin"].InnerText, objXmlMetatype["intmax"].InnerText, objXmlMetatype["intaug"].InnerText);
					lblLOG.Text = string.Format("{0}/{1} ({2})", objXmlMetatype["logmin"].InnerText, objXmlMetatype["logmax"].InnerText, objXmlMetatype["logaug"].InnerText);
					lblWIL.Text = string.Format("{0}/{1} ({2})", objXmlMetatype["wilmin"].InnerText, objXmlMetatype["wilmax"].InnerText, objXmlMetatype["wilaug"].InnerText);
					lblINI.Text = string.Format("{0}/{1} ({2})", objXmlMetatype["inimin"].InnerText, objXmlMetatype["inimax"].InnerText, objXmlMetatype["iniaug"].InnerText);
				}
				else
				{
					lblBOD.Text = objXmlMetatype["bodmin"].InnerText;
					lblAGI.Text = objXmlMetatype["agimin"].InnerText;
					lblREA.Text = objXmlMetatype["reamin"].InnerText;
					lblSTR.Text = objXmlMetatype["strmin"].InnerText;
					lblCHA.Text = objXmlMetatype["chamin"].InnerText;
					lblINT.Text = objXmlMetatype["intmin"].InnerText;
					lblLOG.Text = objXmlMetatype["logmin"].InnerText;
					lblWIL.Text = objXmlMetatype["wilmin"].InnerText;
					lblINI.Text = objXmlMetatype["inimin"].InnerText;
				}

				List<ListItem> lstMetavariants = new List<ListItem>();
				ListItem objNone = new ListItem();
				objNone.Value = "None";
				objNone.Name = LanguageManager.Instance.GetString("String_None");
				lstMetavariants.Add(objNone);

				// Retrieve the list of Metavariants for the selected Metatype.
				XmlNodeList objXmlMetavariantList = objXmlMetatype.SelectNodes("metavariants/metavariant[" + _objCharacter.Options.BookXPath() + "]");
				foreach (XmlNode objXmlMetavariant in objXmlMetavariantList)
				{
					ListItem objMetavariant = new ListItem();
					objMetavariant.Value = objXmlMetavariant["name"].InnerText;
					if (objXmlMetavariant["translate"] != null)
						objMetavariant.Name = objXmlMetavariant["translate"].InnerText;
					else
						objMetavariant.Name = objXmlMetavariant["name"].InnerText;
					lstMetavariants.Add(objMetavariant);
				}

				cboMetavariant.ValueMember = "Value";
				cboMetavariant.DisplayMember = "Name";
				cboMetavariant.DataSource = lstMetavariants;

				// Select the None item.
				cboMetavariant.SelectedIndex = 0;
				lblMetavariantBP.Text = "0";

				// If the Metatype has Force enabled, show the Force NUD.
				if (objXmlMetatype["forcecreature"] != null || objXmlMetatype["essmax"].InnerText.Contains("D6"))
				{
					lblForceLabel.Visible = true;
					nudForce.Visible = true;

					if (objXmlMetatype["essmax"].InnerText.Contains("D6"))
					{
						int intPos = objXmlMetatype["essmax"].InnerText.IndexOf("D6") - 1;
						lblForceLabel.Text = objXmlMetatype["essmax"].InnerText.Substring(intPos, 3);
						nudForce.Maximum = Convert.ToInt32(objXmlMetatype["essmax"].InnerText.Substring(intPos, 1)) * 6;
					}
					else
					{
						lblForceLabel.Text = LanguageManager.Instance.GetString("String_Force");
						nudForce.Maximum = 100;
					}
				}
				else
				{
					lblForceLabel.Visible = false;
					nudForce.Visible = false;
				}
			}
			else
			{
				// Clear the Metavariant list if nothing is currently selected.
				List<ListItem> lstMetavariants = new List<ListItem>();
				ListItem objNone = new ListItem();
				objNone.Value = "None";
				objNone.Name = LanguageManager.Instance.GetString("String_None");
				lstMetavariants.Add(objNone);

				cboMetavariant.ValueMember = "Value";
				cboMetavariant.DisplayMember = "Name";
				cboMetavariant.DataSource = lstMetavariants;
			}
        }

        private void lstMetatypes_DoubleClick(object sender, EventArgs e)
        {
            MetatypeSelected();
        }

        private void cmdOK_Click(object sender, EventArgs e)
        {
            MetatypeSelected();
        }

		private void cboMetavariant_SelectedIndexChanged(object sender, EventArgs e)
		{
			XmlDocument objXmlDocument = XmlManager.Instance.Load(_strXmlFile);
			XmlDocument objXmlQualityDocument = XmlManager.Instance.Load("qualities.xml");

			if (cboMetavariant.SelectedValue.ToString() != "None")
			{
				XmlNode objXmlMetavariant = objXmlDocument.SelectSingleNode("/chummer/metatypes/metatype[name = \"" + lstMetatypes.SelectedValue + "\"]/metavariants/metavariant[name = \"" + cboMetavariant.SelectedValue + "\"]");
				lblMetavariantBP.Text = objXmlMetavariant["bp"].InnerText;

				string strQualities = "";
				// Build a list of the Metavariant's Positive Qualities.
				foreach (XmlNode objXmlQuality in objXmlMetavariant.SelectNodes("qualities/positive/quality"))
				{
					try
					{
						if (GlobalOptions.Instance.Language != "en-us")
						{
							XmlNode objQuality = objXmlQualityDocument.SelectSingleNode("/chummer/qualities/quality[name = \"" + objXmlQuality.InnerText + "\"]");
							if (objQuality["translate"] != null)
								strQualities += objQuality["translate"].InnerText;
							else
								strQualities += objXmlQuality.InnerText;

							if (objXmlQuality.Attributes["select"].InnerText != "")
								strQualities += " (" + LanguageManager.Instance.TranslateExtra(objXmlQuality.Attributes["select"].InnerText) + ")";
						}
						else
						{
							strQualities += objXmlQuality.InnerText;
							if (objXmlQuality.Attributes["select"].InnerText != "")
								strQualities += " (" + objXmlQuality.Attributes["select"].InnerText + ")";
						}
					}
					catch
					{
					}
					strQualities += "\n";
				}
				// Build a list of the Metavariant's Negative Qualities.
				foreach (XmlNode objXmlQuality in objXmlMetavariant.SelectNodes("qualities/negative/quality"))
				{
					try
					{
						if (GlobalOptions.Instance.Language != "en-us")
						{
							XmlNode objQuality = objXmlQualityDocument.SelectSingleNode("/chummer/qualities/quality[name = \"" + objXmlQuality.InnerText + "\"]");
							if (objQuality["translate"] != null)
								strQualities += objQuality["translate"].InnerText;
							else
								strQualities += objXmlQuality.InnerText;

							if (objXmlQuality.Attributes["select"].InnerText != "")
								strQualities += " (" + LanguageManager.Instance.TranslateExtra(objXmlQuality.Attributes["select"].InnerText) + ")";
						}
						else
						{
							strQualities += objXmlQuality.InnerText;
							if (objXmlQuality.Attributes["select"].InnerText != "")
								strQualities += " (" + objXmlQuality.Attributes["select"].InnerText + ")";
						}
					}
					catch
					{
					}
					strQualities += "\n";
				}
				lblMetavariantQualities.Text = strQualities;
			}
			else
			{
				lblMetavariantBP.Text = "0";
				lblMetavariantQualities.Text = "None";
			}
		}

		private void cmdCancel_Click(object sender, EventArgs e)
		{
			this.DialogResult = DialogResult.Cancel;
			this.Close();
		}

		private void cboCategory_SelectedIndexChanged(object sender, EventArgs e)
		{
			PopulateMetatypes();
		}
		#endregion

		#region Custom Methods
		/// <summary>
		/// A Metatype has been selected, so fill in all of the necessary Character information.
		/// </summary>
        void MetatypeSelected()
        {
            if (lstMetatypes.Text != "")
            {
				ImprovementManager objImprovementManager = new ImprovementManager(_objCharacter);
                XmlDocument objXmlDocument = XmlManager.Instance.Load(_strXmlFile);

                XmlNode objXmlMetatype = objXmlDocument.SelectSingleNode("/chummer/metatypes/metatype[name = \"" + lstMetatypes.SelectedValue + "\"]");

				int intForce = 0;
				if (nudForce.Visible)
					intForce = Convert.ToInt32(nudForce.Value);

                // Set Metatype information.
				if (_strXmlFile != "critters.xml" || lstMetatypes.SelectedValue.ToString() == "Ally Spirit")
				{
					_objCharacter.BOD.AssignLimits(ExpressionToString(objXmlMetatype["bodmin"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["bodmax"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["bodaug"].InnerText, intForce, 0));
					_objCharacter.AGI.AssignLimits(ExpressionToString(objXmlMetatype["agimin"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["agimax"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["agiaug"].InnerText, intForce, 0));
					_objCharacter.REA.AssignLimits(ExpressionToString(objXmlMetatype["reamin"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["reamax"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["reaaug"].InnerText, intForce, 0));
					_objCharacter.STR.AssignLimits(ExpressionToString(objXmlMetatype["strmin"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["strmax"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["straug"].InnerText, intForce, 0));
					_objCharacter.CHA.AssignLimits(ExpressionToString(objXmlMetatype["chamin"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["chamax"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["chaaug"].InnerText, intForce, 0));
					_objCharacter.INT.AssignLimits(ExpressionToString(objXmlMetatype["intmin"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["intmax"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["intaug"].InnerText, intForce, 0));
					_objCharacter.LOG.AssignLimits(ExpressionToString(objXmlMetatype["logmin"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["logmax"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["logaug"].InnerText, intForce, 0));
					_objCharacter.WIL.AssignLimits(ExpressionToString(objXmlMetatype["wilmin"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["wilmax"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["wilaug"].InnerText, intForce, 0));
					_objCharacter.INI.AssignLimits(ExpressionToString(objXmlMetatype["inimin"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["inimax"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["iniaug"].InnerText, intForce, 0));
					_objCharacter.MAG.AssignLimits(ExpressionToString(objXmlMetatype["magmin"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["magmax"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["magaug"].InnerText, intForce, 0));
					_objCharacter.RES.AssignLimits(ExpressionToString(objXmlMetatype["resmin"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["resmax"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["resaug"].InnerText, intForce, 0));
					_objCharacter.EDG.AssignLimits(ExpressionToString(objXmlMetatype["edgmin"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["edgmax"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["edgaug"].InnerText, intForce, 0));
					_objCharacter.ESS.AssignLimits(ExpressionToString(objXmlMetatype["essmin"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["essmax"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["essaug"].InnerText, intForce, 0));
				}
				else
				{
					int intMinModifier = -3;
					if (cboCategory.SelectedValue.ToString() == "Mutant Critters")
						intMinModifier = 0;
					_objCharacter.BOD.AssignLimits(ExpressionToString(objXmlMetatype["bodmin"].InnerText, intForce, intMinModifier), ExpressionToString(objXmlMetatype["bodmin"].InnerText, intForce, 3), ExpressionToString(objXmlMetatype["bodmin"].InnerText, intForce, 3));
					_objCharacter.AGI.AssignLimits(ExpressionToString(objXmlMetatype["agimin"].InnerText, intForce, intMinModifier), ExpressionToString(objXmlMetatype["agimin"].InnerText, intForce, 3), ExpressionToString(objXmlMetatype["agimin"].InnerText, intForce, 3));
					_objCharacter.REA.AssignLimits(ExpressionToString(objXmlMetatype["reamin"].InnerText, intForce, intMinModifier), ExpressionToString(objXmlMetatype["reamin"].InnerText, intForce, 3), ExpressionToString(objXmlMetatype["reamin"].InnerText, intForce, 3));
					_objCharacter.STR.AssignLimits(ExpressionToString(objXmlMetatype["strmin"].InnerText, intForce, intMinModifier), ExpressionToString(objXmlMetatype["strmin"].InnerText, intForce, 3), ExpressionToString(objXmlMetatype["strmin"].InnerText, intForce, 3));
					_objCharacter.CHA.AssignLimits(ExpressionToString(objXmlMetatype["chamin"].InnerText, intForce, intMinModifier), ExpressionToString(objXmlMetatype["chamin"].InnerText, intForce, 3), ExpressionToString(objXmlMetatype["chamin"].InnerText, intForce, 3));
					_objCharacter.INT.AssignLimits(ExpressionToString(objXmlMetatype["intmin"].InnerText, intForce, intMinModifier), ExpressionToString(objXmlMetatype["intmin"].InnerText, intForce, 3), ExpressionToString(objXmlMetatype["intmin"].InnerText, intForce, 3));
					_objCharacter.LOG.AssignLimits(ExpressionToString(objXmlMetatype["logmin"].InnerText, intForce, intMinModifier), ExpressionToString(objXmlMetatype["logmin"].InnerText, intForce, 3), ExpressionToString(objXmlMetatype["logmin"].InnerText, intForce, 3));
					_objCharacter.WIL.AssignLimits(ExpressionToString(objXmlMetatype["wilmin"].InnerText, intForce, intMinModifier), ExpressionToString(objXmlMetatype["wilmin"].InnerText, intForce, 3), ExpressionToString(objXmlMetatype["wilmin"].InnerText, intForce, 3));
					_objCharacter.INI.AssignLimits(ExpressionToString(objXmlMetatype["inimin"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["inimax"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["iniaug"].InnerText, intForce, 0));
					_objCharacter.MAG.AssignLimits(ExpressionToString(objXmlMetatype["magmin"].InnerText, intForce, intMinModifier), ExpressionToString(objXmlMetatype["magmin"].InnerText, intForce, 3), ExpressionToString(objXmlMetatype["magmin"].InnerText, intForce, 3));
					_objCharacter.RES.AssignLimits(ExpressionToString(objXmlMetatype["resmin"].InnerText, intForce, intMinModifier), ExpressionToString(objXmlMetatype["resmin"].InnerText, intForce, 3), ExpressionToString(objXmlMetatype["resmin"].InnerText, intForce, 3));
					_objCharacter.EDG.AssignLimits(ExpressionToString(objXmlMetatype["edgmin"].InnerText, intForce, intMinModifier), ExpressionToString(objXmlMetatype["edgmin"].InnerText, intForce, 3), ExpressionToString(objXmlMetatype["edgmin"].InnerText, intForce, 3));
					_objCharacter.ESS.AssignLimits(ExpressionToString(objXmlMetatype["essmin"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["essmax"].InnerText, intForce, 0), ExpressionToString(objXmlMetatype["essaug"].InnerText, intForce, 0));
				}

				// If we're working with a Critter, set the Attributes to their default values.
				if (_strXmlFile == "critters.xml")
				{
					_objCharacter.BOD.Value = Convert.ToInt32(ExpressionToString(objXmlMetatype["bodmin"].InnerText, intForce, 0));
					_objCharacter.AGI.Value = Convert.ToInt32(ExpressionToString(objXmlMetatype["agimin"].InnerText, intForce, 0));
					_objCharacter.REA.Value = Convert.ToInt32(ExpressionToString(objXmlMetatype["reamin"].InnerText, intForce, 0));
					_objCharacter.STR.Value = Convert.ToInt32(ExpressionToString(objXmlMetatype["strmin"].InnerText, intForce, 0));
					_objCharacter.CHA.Value = Convert.ToInt32(ExpressionToString(objXmlMetatype["chamin"].InnerText, intForce, 0));
					_objCharacter.INT.Value = Convert.ToInt32(ExpressionToString(objXmlMetatype["intmin"].InnerText, intForce, 0));
					_objCharacter.LOG.Value = Convert.ToInt32(ExpressionToString(objXmlMetatype["logmin"].InnerText, intForce, 0));
					_objCharacter.WIL.Value = Convert.ToInt32(ExpressionToString(objXmlMetatype["wilmin"].InnerText, intForce, 0));
					_objCharacter.MAG.Value = Convert.ToInt32(ExpressionToString(objXmlMetatype["magmin"].InnerText, intForce, 0));
					_objCharacter.RES.Value = Convert.ToInt32(ExpressionToString(objXmlMetatype["resmin"].InnerText, intForce, 0));
					_objCharacter.EDG.Value = Convert.ToInt32(ExpressionToString(objXmlMetatype["edgmin"].InnerText, intForce, 0));
					_objCharacter.ESS.Value = Convert.ToInt32(ExpressionToString(objXmlMetatype["essmax"].InnerText, intForce, 0));
				}

				// Sprites can never have Physical Attributes or WIL.
				if (lstMetatypes.SelectedValue.ToString().EndsWith("Sprite"))
				{
					_objCharacter.BOD.AssignLimits("0", "0", "0");
					_objCharacter.AGI.AssignLimits("0", "0", "0");
					_objCharacter.REA.AssignLimits("0", "0", "0");
					_objCharacter.STR.AssignLimits("0", "0", "0");
					_objCharacter.WIL.AssignLimits("0", "0", "0");
					_objCharacter.INI.MetatypeMinimum = Convert.ToInt32(ExpressionToString(objXmlMetatype["inimax"].InnerText, intForce, 0));
					_objCharacter.INI.MetatypeMaximum = Convert.ToInt32(ExpressionToString(objXmlMetatype["inimax"].InnerText, intForce, 0));
				}
				
				// If this is a Shapeshifter, a Metavariant must be selected. Default to Human if None is selected.
				if (cboCategory.SelectedValue.ToString() == "Shapeshifter" && cboMetavariant.SelectedValue.ToString() == "None")
					cboMetavariant.SelectedValue = "Human";

				_objCharacter.Metatype = lstMetatypes.SelectedValue.ToString();
				_objCharacter.MetatypeCategory = cboCategory.SelectedValue.ToString();
				if (cboMetavariant.SelectedValue.ToString() == "None")
				{
					_objCharacter.Metavariant = "";
					_objCharacter.MetatypeBP = Convert.ToInt32(lblBP.Text);
				}
				else
				{
					_objCharacter.Metavariant = cboMetavariant.SelectedValue.ToString();
					_objCharacter.MetatypeBP = Convert.ToInt32(lblMetavariantBP.Text);
				}

				if (objXmlMetatype["movement"] != null)
					_objCharacter.Movement = objXmlMetatype["movement"].InnerText;
				// Load the Qualities file.
				XmlDocument objXmlQualityDocument = XmlManager.Instance.Load("qualities.xml");

				if (cboMetavariant.SelectedValue.ToString() == "None")
				{
					// Determine if the Metatype has any bonuses.
					if (objXmlMetatype.InnerXml.Contains("bonus"))
						objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Metatype, lstMetatypes.SelectedValue.ToString(), objXmlMetatype.SelectSingleNode("bonus"), false, 1, lstMetatypes.SelectedValue.ToString());

					// Create the Qualities that come with the Metatype.
					foreach (XmlNode objXmlQualityItem in objXmlMetatype.SelectNodes("qualities/positive/quality"))
					{
						XmlNode objXmlQuality = objXmlQualityDocument.SelectSingleNode("/chummer/qualities/quality[name = \"" + objXmlQualityItem.InnerText + "\"]");
						TreeNode objNode = new TreeNode();
						List<Weapon> objWeapons = new List<Weapon>();
						List<TreeNode> objWeaponNodes = new List<TreeNode>();
						Quality objQuality = new Quality(_objCharacter);
						string strForceValue = "";
						if (objXmlQualityItem.Attributes["select"] != null)
							strForceValue = objXmlQualityItem.Attributes["select"].InnerText;
						QualitySource objSource = new QualitySource();
						objSource = QualitySource.Metatype;
						if (objXmlQualityItem.Attributes["removable"] != null)
							objSource = QualitySource.MetatypeRemovable;
						objQuality.Create(objXmlQuality, _objCharacter, objSource, objNode, objWeapons, objWeaponNodes, strForceValue);
						_objCharacter.Qualities.Add(objQuality);
						
						// Add any created Weapons to the character.
						foreach (Weapon objWeapon in objWeapons)
							_objCharacter.Weapons.Add(objWeapon);
					}
					foreach (XmlNode objXmlQualityItem in objXmlMetatype.SelectNodes("qualities/negative/quality"))
					{
						XmlNode objXmlQuality = objXmlQualityDocument.SelectSingleNode("/chummer/qualities/quality[name = \"" + objXmlQualityItem.InnerText + "\"]");
						TreeNode objNode = new TreeNode();
						List<Weapon> objWeapons = new List<Weapon>();
						List<TreeNode> objWeaponNodes = new List<TreeNode>();
						Quality objQuality = new Quality(_objCharacter);
						string strForceValue = "";
						if (objXmlQualityItem.Attributes["select"] != null)
							strForceValue = objXmlQualityItem.Attributes["select"].InnerText;
						QualitySource objSource = new QualitySource();
						objSource = QualitySource.Metatype;
						if (objXmlQualityItem.Attributes["removable"] != null)
							objSource = QualitySource.MetatypeRemovable;
						objQuality.Create(objXmlQuality, _objCharacter, objSource, objNode, objWeapons, objWeaponNodes, strForceValue);
						_objCharacter.Qualities.Add(objQuality);

						// Add any created Weapons to the character.
						foreach (Weapon objWeapon in objWeapons)
							_objCharacter.Weapons.Add(objWeapon);
					}
				}

				// If a Metavariant has been selected, locate it in the file.
				if (cboMetavariant.SelectedValue.ToString() != "None")
				{
					XmlNode objXmlMetavariant = objXmlMetatype.SelectSingleNode("metavariants/metavariant[name = \"" + cboMetavariant.SelectedValue + "\"]");
					// Determine if the Metavariant has any bonuses.
					if (objXmlMetavariant.InnerXml.Contains("bonus"))
						objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Metavariant, cboMetavariant.SelectedValue.ToString(), objXmlMetavariant.SelectSingleNode("bonus"), false, 1, cboMetavariant.SelectedValue.ToString());

					// Create the Qualities that come with the Metatype.
					foreach (XmlNode objXmlQualityItem in objXmlMetavariant.SelectNodes("qualities/positive/quality"))
					{
						XmlNode objXmlQuality = objXmlQualityDocument.SelectSingleNode("/chummer/qualities/quality[name = \"" + objXmlQualityItem.InnerText + "\"]");
						TreeNode objNode = new TreeNode();
						List<Weapon> objWeapons = new List<Weapon>();
						List<TreeNode> objWeaponNodes = new List<TreeNode>();
						Quality objQuality = new Quality(_objCharacter);
						string strForceValue = "";
						if (objXmlQualityItem.Attributes["select"] != null)
							strForceValue = objXmlQualityItem.Attributes["select"].InnerText;
						QualitySource objSource = new QualitySource();
						objSource = QualitySource.Metatype;
						if (objXmlQualityItem.Attributes["removable"] != null)
							objSource = QualitySource.MetatypeRemovable;
						objQuality.Create(objXmlQuality, _objCharacter, objSource, objNode, objWeapons, objWeaponNodes, strForceValue);
						_objCharacter.Qualities.Add(objQuality);

						// Add any created Weapons to the character.
						foreach (Weapon objWeapon in objWeapons)
							_objCharacter.Weapons.Add(objWeapon);
					}
					foreach (XmlNode objXmlQualityItem in objXmlMetavariant.SelectNodes("qualities/negative/quality"))
					{
						XmlNode objXmlQuality = objXmlQualityDocument.SelectSingleNode("/chummer/qualities/quality[name = \"" + objXmlQualityItem.InnerText + "\"]");
						TreeNode objNode = new TreeNode();
						List<Weapon> objWeapons = new List<Weapon>();
						List<TreeNode> objWeaponNodes = new List<TreeNode>();
						Quality objQuality = new Quality(_objCharacter);
						string strForceValue = "";
						if (objXmlQualityItem.Attributes["select"] != null)
							strForceValue = objXmlQualityItem.Attributes["select"].InnerText;
						QualitySource objSource = new QualitySource();
						objSource = QualitySource.Metatype;
						if (objXmlQualityItem.Attributes["removable"] != null)
							objSource = QualitySource.MetatypeRemovable;
						objQuality.Create(objXmlQuality, _objCharacter, objSource, objNode, objWeapons, objWeaponNodes, strForceValue);
						_objCharacter.Qualities.Add(objQuality);

						// Add any created Weapons to the character.
						foreach (Weapon objWeapon in objWeapons)
							_objCharacter.Weapons.Add(objWeapon);
					}
				}

				// Run through the character's Attributes one more time and make sure their value matches their minimum value.
				if (_strXmlFile == "metatypes.xml")
				{
					_objCharacter.BOD.Value = _objCharacter.BOD.TotalMinimum;
					_objCharacter.AGI.Value = _objCharacter.AGI.TotalMinimum;
					_objCharacter.REA.Value = _objCharacter.REA.TotalMinimum;
					_objCharacter.STR.Value = _objCharacter.STR.TotalMinimum;
					_objCharacter.CHA.Value = _objCharacter.CHA.TotalMinimum;
					_objCharacter.INT.Value = _objCharacter.INT.TotalMinimum;
					_objCharacter.LOG.Value = _objCharacter.LOG.TotalMinimum;
					_objCharacter.WIL.Value = _objCharacter.WIL.TotalMinimum;
				}

				// Add any Critter Powers the Metatype/Critter should have.
				XmlNode objXmlCritter = objXmlDocument.SelectSingleNode("/chummer/metatypes/metatype[name = \"" + _objCharacter.Metatype + "\"]");

				objXmlDocument = XmlManager.Instance.Load("critterpowers.xml");
				foreach (XmlNode objXmlPower in objXmlCritter.SelectNodes("powers/power"))
				{
					XmlNode objXmlCritterPower = objXmlDocument.SelectSingleNode("/chummer/powers/power[name = \"" + objXmlPower.InnerText + "\"]");
					TreeNode objNode = new TreeNode();
					CritterPower objPower = new CritterPower(_objCharacter);
					string strForcedValue = "";
					int intRating = 0;

					if (objXmlPower.Attributes["rating"] != null)
						intRating = Convert.ToInt32(objXmlPower.Attributes["rating"].InnerText);
					if (objXmlPower.Attributes["select"] != null)
						strForcedValue = objXmlPower.Attributes["select"].InnerText;

					objPower.Create(objXmlCritterPower, _objCharacter, objNode, intRating, strForcedValue);
					objPower.CountTowardsLimit = false;
					_objCharacter.CritterPowers.Add(objPower);
				}

				// Add any Critter Powers the Metavariant should have.
				if (cboMetavariant.SelectedValue.ToString() != "None")
				{
					XmlNode objXmlMetavariant = objXmlMetatype.SelectSingleNode("metavariants/metavariant[name = \"" + cboMetavariant.SelectedValue + "\"]");
					foreach (XmlNode objXmlPower in objXmlMetavariant.SelectNodes("powers/power"))
					{
						XmlNode objXmlCritterPower = objXmlDocument.SelectSingleNode("/chummer/powers/power[name = \"" + objXmlPower.InnerText + "\"]");
						TreeNode objNode = new TreeNode();
						CritterPower objPower = new CritterPower(_objCharacter);
						string strForcedValue = "";
						int intRating = 0;

						if (objXmlPower.Attributes["rating"] != null)
							intRating = Convert.ToInt32(objXmlPower.Attributes["rating"].InnerText);
						if (objXmlPower.Attributes["select"] != null)
							strForcedValue = objXmlPower.Attributes["select"].InnerText;

						objPower.Create(objXmlCritterPower, _objCharacter, objNode, intRating, strForcedValue);
						objPower.CountTowardsLimit = false;
						_objCharacter.CritterPowers.Add(objPower);
					}
				}

				// If this is a Blood Spirit, add their free Critter Powers.
				if (chkBloodSpirit.Checked)
				{
					XmlNode objXmlCritterPower;
					TreeNode objNode;
					CritterPower objPower;
					bool blnAddPower = true;

					// Energy Drain.
					foreach (CritterPower objFindPower in _objCharacter.CritterPowers)
					{
						if (objFindPower.Name == "Energy Drain")
						{
							blnAddPower = false;
							break;
						}
					}
					if (blnAddPower)
					{
						objXmlCritterPower = objXmlDocument.SelectSingleNode("/chummer/powers/power[name = \"Energy Drain\"]");
						objNode = new TreeNode();
						objPower = new CritterPower(_objCharacter);
						objPower.Create(objXmlCritterPower, _objCharacter, objNode, 0, "");
						objPower.CountTowardsLimit = false;
						_objCharacter.CritterPowers.Add(objPower);
					}

					// Fear.
					blnAddPower = true;
					foreach (CritterPower objFindPower in _objCharacter.CritterPowers)
					{
						if (objFindPower.Name == "Fear")
						{
							blnAddPower = false;
							break;
						}
					}
					if (blnAddPower)
					{
						objXmlCritterPower = objXmlDocument.SelectSingleNode("/chummer/powers/power[name = \"Fear\"]");
						objNode = new TreeNode();
						objPower = new CritterPower(_objCharacter);
						objPower.Create(objXmlCritterPower, _objCharacter, objNode, 0, "");
						objPower.CountTowardsLimit = false;
						_objCharacter.CritterPowers.Add(objPower);
					}

					// Natural Weapon.
					objXmlCritterPower = objXmlDocument.SelectSingleNode("/chummer/powers/power[name = \"Natural Weapon\"]");
					objNode = new TreeNode();
					objPower = new CritterPower(_objCharacter);
					objPower.Create(objXmlCritterPower, _objCharacter, objNode, 0, "DV " + intForce.ToString() + "P, AP 0");
					objPower.CountTowardsLimit = false;
					_objCharacter.CritterPowers.Add(objPower);

					// Evanescence.
					blnAddPower = true;
					foreach (CritterPower objFindPower in _objCharacter.CritterPowers)
					{
						if (objFindPower.Name == "Evanescence")
						{
							blnAddPower = false;
							break;
						}
					}
					if (blnAddPower)
					{
						objXmlCritterPower = objXmlDocument.SelectSingleNode("/chummer/powers/power[name = \"Evanescence\"]");
						objNode = new TreeNode();
						objPower = new CritterPower(_objCharacter);
						objPower.Create(objXmlCritterPower, _objCharacter, objNode, 0, "");
						objPower.CountTowardsLimit = false;
						_objCharacter.CritterPowers.Add(objPower);
					}
				}

				// Remove the Critter's Materialization Power if they have it. Add the Possession or Inhabitation Power if the Possession-based Tradition checkbox is checked.
				if (chkPossessionBased.Checked)
				{
					foreach (CritterPower objCritterPower in _objCharacter.CritterPowers)
					{
						if (objCritterPower.Name == "Materialization")
						{
							_objCharacter.CritterPowers.Remove(objCritterPower);
							break;
						}
					}

					// Add the selected Power.
					XmlNode objXmlCritterPower = objXmlDocument.SelectSingleNode("/chummer/powers/power[name = \"" + cboPossessionMethod.SelectedValue.ToString() + "\"]");
					TreeNode objNode = new TreeNode();
					CritterPower objPower = new CritterPower(_objCharacter);
					objPower.Create(objXmlCritterPower, _objCharacter, objNode, 0, "");
					objPower.CountTowardsLimit = false;
					_objCharacter.CritterPowers.Add(objPower);
				}

				// Set the Skill Ratings for the Critter.
				foreach (XmlNode objXmlSkill in objXmlCritter.SelectNodes("skills/skill"))
				{
					if (objXmlSkill.InnerText.Contains("Exotic"))
					{
						Skill objExotic = new Skill(_objCharacter);
						objExotic.ExoticSkill = true;
						objExotic.Attribute = "AGI";
						if (objXmlSkill.Attributes["spec"] != null)
							objExotic.Specialization = objXmlSkill.Attributes["spec"].InnerText;
						if (Convert.ToInt32(ExpressionToString(objXmlSkill.Attributes["rating"].InnerText, Convert.ToInt32(nudForce.Value), 0)) > 6)
							objExotic.RatingMaximum = Convert.ToInt32(ExpressionToString(objXmlSkill.Attributes["rating"].InnerText, Convert.ToInt32(nudForce.Value), 0));
						objExotic.Rating = Convert.ToInt32(ExpressionToString(objXmlSkill.Attributes["rating"].InnerText, Convert.ToInt32(nudForce.Value), 0));
						objExotic.Name = objXmlSkill.InnerText;
						_objCharacter.Skills.Add(objExotic);
					}
					else
					{
						foreach (Skill objSkill in _objCharacter.Skills)
						{
							if (objSkill.Name == objXmlSkill.InnerText)
							{
								if (objXmlSkill.Attributes["spec"] != null)
									objSkill.Specialization = objXmlSkill.Attributes["spec"].InnerText;
								if (Convert.ToInt32(ExpressionToString(objXmlSkill.Attributes["rating"].InnerText, Convert.ToInt32(nudForce.Value), 0)) > 6)
									objSkill.RatingMaximum = Convert.ToInt32(ExpressionToString(objXmlSkill.Attributes["rating"].InnerText, Convert.ToInt32(nudForce.Value), 0));
								objSkill.Rating = Convert.ToInt32(ExpressionToString(objXmlSkill.Attributes["rating"].InnerText, Convert.ToInt32(nudForce.Value), 0));
								break;
							}
						}
					}
				}

				// Set the Skill Group Ratings for the Critter.
				foreach (XmlNode objXmlSkill in objXmlCritter.SelectNodes("skills/group"))
				{
					foreach (SkillGroup objSkill in _objCharacter.SkillGroups)
					{
						if (objSkill.Name == objXmlSkill.InnerText)
						{
							objSkill.RatingMaximum = Convert.ToInt32(ExpressionToString(objXmlSkill.Attributes["rating"].InnerText, Convert.ToInt32(nudForce.Value), 0));
							objSkill.Rating = Convert.ToInt32(ExpressionToString(objXmlSkill.Attributes["rating"].InnerText, Convert.ToInt32(nudForce.Value), 0));
							break;
						}
					}
				}

				// Set the Knowledge Skill Ratings for the Critter.
				foreach (XmlNode objXmlSkill in objXmlCritter.SelectNodes("skills/knowledge"))
				{
					Skill objKnowledge = new Skill(_objCharacter);
					objKnowledge.Name = objXmlSkill.InnerText;
					objKnowledge.KnowledgeSkill = true;
					if (objXmlSkill.Attributes["spec"] != null)
						objKnowledge.Specialization = objXmlSkill.Attributes["spec"].InnerText;
					objKnowledge.SkillCategory = objXmlSkill.Attributes["category"].InnerText;
					if (Convert.ToInt32(objXmlSkill.Attributes["rating"].InnerText) > 6)
						objKnowledge.RatingMaximum = Convert.ToInt32(objXmlSkill.Attributes["rating"].InnerText);
					objKnowledge.Rating = Convert.ToInt32(objXmlSkill.Attributes["rating"].InnerText);
					_objCharacter.Skills.Add(objKnowledge);
				}

				// If this is a Critter with a Force (which dictates their Skill Rating/Maximum Skill Rating), set their Skill Rating Maximums.
				if (intForce > 0)
				{
					int intMaxRating = intForce;
					// Determine the highest Skill Rating the Critter has.
					foreach (Skill objSkill in _objCharacter.Skills)
					{
						if (objSkill.RatingMaximum > intMaxRating)
							intMaxRating = objSkill.RatingMaximum;
					}

					// Now that we know the upper limit, set all of the Skill Rating Maximums to match.
					foreach (Skill objSkill in _objCharacter.Skills)
						objSkill.RatingMaximum = intMaxRating;
					foreach (SkillGroup objGroup in _objCharacter.SkillGroups)
						objGroup.RatingMaximum = intMaxRating;

					// Set the MaxSkillRating for the character so it can be used later when they add new Knowledge Skills or Exotic Skills.
					_objCharacter.MaxSkillRating = intMaxRating;
				}

            	// Add any Complex Forms the Critter comes with (typically Sprites)
				XmlDocument objXmlProgramDocument = XmlManager.Instance.Load("programs.xml");
				foreach (XmlNode objXmlComplexForm in objXmlCritter.SelectNodes("complexforms/complexform"))
				{
					int intRating = 0;
					if (objXmlComplexForm.Attributes["rating"] != null)
						intRating = Convert.ToInt32(ExpressionToString(objXmlComplexForm.Attributes["rating"].InnerText, Convert.ToInt32(nudForce.Value), 0));
					string strForceValue = "";
					if (objXmlComplexForm.Attributes["select"] != null)
						strForceValue = objXmlComplexForm.Attributes["select"].InnerText;
					XmlNode objXmlProgram = objXmlProgramDocument.SelectSingleNode("/chummer/programs/program[name = \"" + objXmlComplexForm.InnerText + "\"]");
					TreeNode objNode = new TreeNode();
					TechProgram objProgram = new TechProgram(_objCharacter);
					objProgram.Create(objXmlProgram, _objCharacter, objNode, strForceValue);
					objProgram.Rating = intRating;
					_objCharacter.TechPrograms.Add(objProgram);

					// Add the Program Option if applicable.
					if (objXmlComplexForm.Attributes["option"] != null)
					{
						int intOptRating = 0;
						if (objXmlComplexForm.Attributes["optionrating"] != null)
							intOptRating = Convert.ToInt32(ExpressionToString(objXmlComplexForm.Attributes["optionrating"].InnerText, Convert.ToInt32(nudForce.Value), 0));
						string strOptForceValue = "";
						if (objXmlComplexForm.Attributes["optionselect"] != null)
							strOptForceValue = objXmlComplexForm.Attributes["optionselect"].InnerText;
						XmlNode objXmlOption = objXmlProgramDocument.SelectSingleNode("/chummer/options/option[name = \"" + objXmlComplexForm.Attributes["option"].InnerText + "\"]");
						TreeNode objNodeOpt = new TreeNode();
						TechProgramOption objOption = new TechProgramOption(_objCharacter);
						objOption.Create(objXmlOption, _objCharacter, objNodeOpt, strOptForceValue);
						objOption.Rating = intOptRating;
						objProgram.Options.Add(objOption);
					}
				}

				// Add any Gear the Critter comes with (typically Programs for A.I.s)
				XmlDocument objXmlGearDocument = XmlManager.Instance.Load("gear.xml");
				foreach (XmlNode objXmlGear in objXmlCritter.SelectNodes("gears/gear"))
				{
					int intRating = 0;
					if (objXmlGear.Attributes["rating"] != null)
						intRating = Convert.ToInt32(ExpressionToString(objXmlGear.Attributes["rating"].InnerText, Convert.ToInt32(nudForce.Value), 0));
					string strForceValue = "";
					if (objXmlGear.Attributes["select"] != null)
						strForceValue = objXmlGear.Attributes["select"].InnerText;
					XmlNode objXmlGearItem = objXmlGearDocument.SelectSingleNode("/chummer/gears/gear[name = \"" + objXmlGear.InnerText + "\"]");
					TreeNode objNode = new TreeNode();
					Gear objGear = new Gear(_objCharacter);
					List<Weapon> lstWeapons = new List<Weapon>();
					List<TreeNode> lstWeaponNodes = new List<TreeNode>();
					objGear.Create(objXmlGearItem, _objCharacter, objNode, intRating, lstWeapons, lstWeaponNodes, strForceValue);
					objGear.Cost = "0";
					objGear.Cost3 = "0";
					objGear.Cost6 = "0";
					objGear.Cost10 = "0";
					_objCharacter.Gear.Add(objGear);
				}

				// If this is a Mutant Critter, count up the number of Skill points they start with.
				if (_objCharacter.MetatypeCategory == "Mutant Critters")
				{
					foreach (Skill objSkill in _objCharacter.Skills)
						_objCharacter.MutantCritterBaseSkills += objSkill.Rating;
				}

				this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show(LanguageManager.Instance.GetString("Message_Metatype_SelectMetatype"), LanguageManager.Instance.GetString("MessageTitle_Metatype_SelectMetatype"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

		/// <summary>
		/// Convert Force, 1D6, or 2D6 into a usable value.
		/// </summary>
		/// <param name="strIn">Expression to convert.</param>
		/// <param name="intForce">Force value to use.</param>
		/// <param name="intOffset">Dice offset.</param>
		/// <returns></returns>
		public string ExpressionToString(string strIn, int intForce, int intOffset)
		{
			int intValue = 0;
			XmlDocument objXmlDocument = new XmlDocument();
			XPathNavigator nav = objXmlDocument.CreateNavigator();
			XPathExpression xprAttribute = nav.Compile(strIn.Replace("/", " div ").Replace("F", intForce.ToString()).Replace("1D6", intForce.ToString()).Replace("2D6", intForce.ToString()));
			// This statement is wrapped in a try/catch since trying 1 div 2 results in an error with XSLT.
			try
			{
                string temp = string.Format(GlobalOptions.Instance.CultureInfo, "{0}", nav.Evaluate(xprAttribute));
                intValue = Convert.ToInt32(temp);
			}
			catch
			{
				intValue = 1;
			}
			intValue += intOffset;
			if (intForce > 0)
			{
				if (intValue < 1)
					intValue = 1;
			}
			else
			{
				if (intValue < 0)
					intValue = 0;
			}
			return intValue.ToString();
		}

        /// <summary>
        /// Populate the list of Metatypes.
        /// </summary>
        void PopulateMetatypes()
        {
            XmlDocument objXmlDocument = XmlManager.Instance.Load(_strXmlFile);
			List<ListItem> lstMetatype = new List<ListItem>();

        	XmlNodeList objXmlMetatypeList = objXmlDocument.SelectNodes("/chummer/metatypes/metatype[(" + _objCharacter.Options.BookXPath() + ") and category = \"" + cboCategory.SelectedValue + "\"]");

            foreach (XmlNode objXmlMetatype in objXmlMetatypeList)
			{
				ListItem objItem = new ListItem();
				objItem.Value = objXmlMetatype["name"].InnerText;
				if (objXmlMetatype["translate"] != null)
					objItem.Name = objXmlMetatype["translate"].InnerText;
				else
					objItem.Name = objXmlMetatype["name"].InnerText;
				lstMetatype.Add(objItem);
            }
			SortListItem objSort = new SortListItem();
			lstMetatype.Sort(objSort.Compare);
			lstMetatypes.DataSource = null;
			lstMetatypes.ValueMember = "Value";
			lstMetatypes.DisplayMember = "Name";
			lstMetatypes.DataSource = lstMetatype;
			lstMetatypes.SelectedIndex = -1;

			if (cboCategory.SelectedValue.ToString().EndsWith("Spirits"))
			{
				chkBloodSpirit.Visible = true;
				chkPossessionBased.Visible = true;
				cboPossessionMethod.Visible = true;
			}
			else
			{
				chkBloodSpirit.Checked = false;
				chkBloodSpirit.Visible = false;
				chkPossessionBased.Visible = false;
				chkPossessionBased.Checked = false;
				cboPossessionMethod.Visible = false;
			}
        }

		private void chkPossessionBased_CheckedChanged(object sender, EventArgs e)
		{
			cboPossessionMethod.Enabled = chkPossessionBased.Checked;
		}
		#endregion
    }
}