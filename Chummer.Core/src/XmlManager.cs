using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace Chummer.Core
{
	public sealed class XmlManager : IXmlResourceLoader
	{
		private static IEnumerable<XmlNode> SelectNodes(XmlNode objNode, string strXPath)
		{
			var objNodes = objNode.SelectNodes(strXPath);
			if (objNodes == null)
				yield break;

			foreach (XmlNode objChild in objNodes)
				yield return objChild;
		}

		/// <summary>
		/// Used to cache XML files so that they do not need to be loaded and translated each time an object wants the file.
		/// </summary>
		private class XmlReference
		{
			private DateTime _datDate = new DateTime();
			private string _strFileName = "";
			private XmlDocument _objXmlDocument = new XmlDocument();

			/// <summary>
			/// Date/Time stamp on the XML file.
			/// </summary>
			public DateTime FileDate
			{
				get
				{
					return _datDate;
				}
				set
				{
					_datDate = value;
				}
			}

			/// <summary>
			/// Name of the XML file.
			/// </summary>
			public string FileName
			{
				get
				{
					return _strFileName;
				}
				set
				{
					_strFileName = value;
				}
			}

			/// <summary>
			/// XmlDocument that is created by merging the base data file and data translation file. Does not include custom content since this must be loaded each time.
			/// </summary>
			public XmlDocument XmlContent
			{
				get
				{
					return _objXmlDocument;
				}
				set
				{
					_objXmlDocument = value;
				}
			}
		}
		
		static readonly XmlManager ObjInstance = new XmlManager();
		static private readonly List<XmlReference> LstXmlDocuments = new List<XmlReference>();

		#region Constructor and Instance
		static XmlManager()
		{
			LanguageManager.Instance.Load(GlobalOptions.Instance.Language);
		}

		XmlManager()
		{
		}

		/// <summary>
		/// Glboal XmlManager instance.
		/// </summary>
		public static XmlManager Instance
		{
			get
			{
				return ObjInstance;
			}
		}
		#endregion

		#region Methods
		/// <summary>
		/// Load the selected XML file and its associated custom file.
		/// </summary>
		/// <param name="strFileName">Name of the XML file to load.</param>
		public XmlDocument Load(string strFileName)
		{
			var strPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "data");
			strPath = Path.Combine(strPath, strFileName);
			var datDate = File.GetLastWriteTime(strPath);

			// Look to see if this XmlDocument is already loaded.
			var blnFound = false;
			var objReference = new XmlReference();
			foreach (var objCurrentReference in LstXmlDocuments)
			{
				if (objCurrentReference.FileName == strFileName)
				{
					objReference = objCurrentReference;
					blnFound = true;
					break;
				}
			}

			var blnLoadFile = false;
			if (!blnFound)
			{
				// The file was not found in the reference list, so it must be loaded.
				blnLoadFile = true;
				LstXmlDocuments.Add(objReference);
			}
			else
			{
				// The file was found in the List, so check the last write time.
				if (datDate != objReference.FileDate)
				{
					// The last write time does not match, so it must be reloaded.
					blnLoadFile = true;
				}
			}

			// Create a new document that everything will be merged into.
			var objDoc = new XmlDocument();
			// write the root chummer node.
			XmlNode objCont = objDoc.CreateElement("chummer");
			objDoc.AppendChild(objCont);

			var objXmlFile = new XmlDocument();
			IEnumerable<XmlNode> objList;

			if (blnLoadFile)
			{
				// Load the base file and retrieve all of the child nodes.
				objXmlFile.Load(strPath);
				objList = SelectNodes(objXmlFile, "/chummer/*");
				foreach (XmlNode objNode in objList)
				{
					// Append the entire child node to the new document.
					var objImported = objDoc.ImportNode(objNode, true);
					objCont.AppendChild(objImported);
				}

				// Load any override data files the user might have. Do not attempt this if we're loading the Improvements file.
				if (strFileName != "improvements.xml")
				{
					var strFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "data");
					foreach (var strFile in Directory.Exists(strFilePath)
						? Directory.GetFiles(strFilePath, "override*_" + strFileName)
						: Array.Empty<string>())
					{
						objXmlFile.Load(strFile);
						objList = SelectNodes(objXmlFile, "/chummer/*");
						foreach (XmlNode objNode in objList)
						{
							foreach (XmlNode objType in objNode.ChildNodes)
							{
								if (objType["name"] != null)
								{
									var objName = objType["name"];
									if (objName != null)
									{
										var objItem = objDoc.SelectSingleNode("/chummer/" + objNode.Name + "/" + objType.Name + "[name = \"" + objName.InnerText.Replace("&amp;", "&") + "\"]");
										if (objItem != null)
											objItem.InnerXml = objType.InnerXml;
									}
								}
							}
						}
					}
				}

				// Load the translation file for the current base data file if the selected language is not en-us.
				if (GlobalOptions.Instance.Language != "en-us")
				{
					// Everything is stored in the selected language file to make translations easier, keep all of the language-specific information together, and not require users to download 27 individual files.
					// The structure is similar to the base data file, but the root node is instead a child /chummer node with a file attribute to indicate the XML file it translates.
					if (LanguageManager.Instance.DataDoc != null)
					{
						foreach (XmlNode objNode in SelectNodes(LanguageManager.Instance.DataDoc, "/chummer/chummer[@file = \"" + strFileName + "\"]"))
						{
							foreach (XmlNode objType in objNode.ChildNodes)
							{
								foreach (XmlNode objChild in objType.ChildNodes)
								{
									// If this is a translatable item, find the proper node and add/update this information.
									if (objChild["translate"] != null)
									{
										var objItem = objDoc.SelectSingleNode("/chummer/" + objType.Name + "/" + objChild.Name + "[name = \"" + objChild["name"].InnerXml.Replace("&amp;", "&") + "\"]");
										if (objItem != null)
											objItem.InnerXml += "<translate>" + objChild["translate"].InnerXml + "</translate>";
									}
									if (objChild["page"] != null)
									{
										var objItem = objDoc.SelectSingleNode("/chummer/" + objType.Name + "/" + objChild.Name + "[name = \"" + objChild["name"].InnerXml.Replace("&amp;", "&") + "\"]");
										if (objItem != null)
											objItem.InnerXml += "<altpage>" + objChild["page"].InnerXml + "</altpage>";
									}
									if (objChild["code"] != null)
									{
										var objItem = objDoc.SelectSingleNode("/chummer/" + objType.Name + "/" + objChild.Name + "[name = \"" + objChild["name"].InnerXml.Replace("&amp;", "&") + "\"]");
										if (objItem != null)
											objItem.InnerXml += "<altcode>" + objChild["code"].InnerXml + "</altcode>";
									}
									if (objChild["advantage"] != null)
									{
										var objItem = objDoc.SelectSingleNode("/chummer/" + objType.Name + "/" + objChild.Name + "[name = \"" + objChild["name"].InnerXml.Replace("&amp;", "&") + "\"]");
										if (objItem != null)
											objItem.InnerXml += "<altadvantage>" + objChild["advantage"].InnerXml + "</altadvantage>";
									}
									if (objChild["disadvantage"] != null)
									{
										var objItem = objDoc.SelectSingleNode("/chummer/" + objType.Name + "/" + objChild.Name + "[name = \"" + objChild["name"].InnerXml.Replace("&amp;", "&") + "\"]");
										if (objItem != null)
											objItem.InnerXml += "<altdisadvantage>" + objChild["disadvantage"].InnerXml + "</altdisadvantage>";
									}
									if (objChild.Attributes != null)
									{
										// Handle Category name translations.
										if (objChild.Attributes["translate"] != null)
										{
											var objItem = objDoc.SelectSingleNode("/chummer/" + objType.Name + "/" + objChild.Name + "[. = \"" + objChild.InnerXml.Replace("&amp;", "&") + "\"]");
											if (objItem != null)
											{
												var objElement = (XmlElement)objItem;
												objElement.SetAttribute("translate", objChild.Attributes["translate"].InnerXml);
											}
										}
									}

									// Check for Skill Specialization information.
									if (strFileName == "skills.xml")
									{
										if (objChild["specs"] != null)
										{
											foreach (XmlNode objSpec in objChild.SelectNodes("specs/spec"))
											{
												if (objSpec.Attributes["translate"] != null)
												{
													var objItem = objDoc.SelectSingleNode("/chummer/" + objType.Name + "/skill[name = \"" + objChild["name"].InnerXml + "\"]/specs/spec[. = \"" + objSpec.InnerXml + "\"]");
													if (objItem != null)
													{
														var objElement = (XmlElement)objItem;
														objElement.SetAttribute("translate", objSpec.Attributes["translate"].InnerXml);
													}
												}
											}
										}
									}

									// Check for Metavariant information.
									if (strFileName == "metatypes.xml")
									{
										if (objChild["metavariants"] != null)
										{
											foreach (XmlNode objMetavariant in objChild.SelectNodes("metavariants/metavariant"))
											{
												if (objMetavariant["translate"] != null)
												{
													var objItem = objDoc.SelectSingleNode("/chummer/metatypes/metatype[name = \"" + objChild["name"].InnerXml + "\"]/metavariants/metavariant[name = \"" + objMetavariant["name"].InnerXml + "\"]");
													if (objItem != null)
														objItem.InnerXml += "<translate>" + objMetavariant["translate"].InnerXml + "</translate>";
												}
												if (objMetavariant["altpage"] != null)
												{
													var objItem = objDoc.SelectSingleNode("/chummer/metatypes/metatype[name = \"" + objChild["name"].InnerXml + "\"]/metavariants/metavariant[name = \"" + objMetavariant["name"].InnerXml + "\"]");
													if (objItem != null)
														objItem.InnerXml += "<altpage>" + objMetavariant["page"].InnerXml + "</altpage>";
												}
											}
										}
									}

									// Check for Martial Art Advantage information.
									if (strFileName == "martialarts.xml")
									{
										if (objChild["advantages"] != null)
										{
											foreach (XmlNode objAdvantage in objChild.SelectNodes("advantages/advantage"))
											{
												if (objAdvantage.Attributes["translate"] != null)
												{
													var objItem = objDoc.SelectSingleNode("/chummer/martialarts/martialart[name = \"" + objChild["name"].InnerXml + "\"]/advantages/advantage[. = \"" + objAdvantage.InnerXml + "\"]");
													if (objItem != null)
													{
														var objElement = (XmlElement)objItem;
														objElement.SetAttribute("translate", objAdvantage.Attributes["translate"].InnerXml);
													}
												}
											}
										}
									}

									// Check for Mentor Spirit/Paragon choice information.
									if (strFileName == "mentors.xml" || strFileName == "paragons.xml")
									{
										if (objChild["choices"] != null)
										{
											foreach (XmlNode objChoice in objChild.SelectNodes("choices/choice"))
											{
												if (objChoice["translate"] != null)
												{
													var objItem = objDoc.SelectSingleNode("/chummer/mentors/mentor[name = \"" + objChild["name"].InnerXml + "\"]/choices/choice[name = \"" + objChoice["name"].InnerXml + "\"]");
													if (objItem != null)
														objItem.InnerXml += "<translate>" + objChoice["translate"].InnerXml + "</translate>";
												}
											}
										}
									}
								}
							}
						}
					}
				}

				// Cache the merged document and its relevant information.
				objReference.FileDate = datDate;
				objReference.FileName = strFileName;
				objReference.XmlContent = objDoc;
			}
			else
			{
				// Pull the document from cache.
				objDoc = objReference.XmlContent;
			}

			// A new XmlDocument is created by loading the a copy of the cached one so that we don't stuff custom content into the cached copy
			// (which we don't want and also results in multiple copies of each custom item).
			var objReturnDocument = new XmlDocument();
			objReturnDocument.LoadXml(objDoc.OuterXml);

			// Load any custom data files the user might have. Do not attempt this if we're loading the Improvements file.
			if (strFileName != "improvements.xml")
			{
				strPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "data");
				foreach (var strFile in Directory.GetFiles(strPath, "custom*_" + strFileName))
				{
					objXmlFile.Load(strFile);
					objList = SelectNodes(objXmlFile, "/chummer/*");
					foreach (XmlNode objNode in objList)
					{
						// Look for any items with a duplicate name and pluck them from the node so we don't end up with multiple items with the same name.
						var lstDelete = new List<XmlNode>();
						foreach (XmlNode objChild in objNode.ChildNodes)
						{
							// Only do this if the child has the name field since this is what we must match on.
							if (objChild["name"] != null)
							{
								var objNodeList = objReturnDocument.SelectNodes("/chummer/" + objChild.ParentNode.Name + "/" + objChild.Name + "[name = \"" + objChild["name"].InnerText + "\"]");
								if (objNodeList.Count > 0)
								{
									lstDelete.Add(objChild);
								}
							}
						}
						// Remove the offending items from the node we're about to merge in.
						foreach (var objRemoveNode in lstDelete)
						{
							objNode.RemoveChild(objRemoveNode);
						}

						// Append the entire child node to the new document.
						var objImported = objReturnDocument.ImportNode(objNode, true);
						objReturnDocument.DocumentElement.AppendChild(objImported);
					}
				}
			}

			return objReturnDocument;
		}

		/// <summary>
		/// Verify the contents of the language data translation file.
		/// </summary>
		/// <param name="strLanguage">Language to check.</param>
		/// <param name="lstBooks">List of books.</param>
		public void Verify(string strLanguage, List<string> lstBooks)
		{
			var objLanguageDoc = new XmlDocument();
			var strFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "lang");
			strFilePath = Path.Combine(strFilePath, strLanguage + "_data.xml");
			objLanguageDoc.Load(strFilePath);

			var strLangPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "lang");
			strLangPath = Path.Combine(strLangPath, "results_" + strLanguage + ".xml");
			var objStream = new FileStream(strLangPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
			var objWriter = new XmlTextWriter(objStream, Encoding.Unicode);
			objWriter.Formatting = Formatting.Indented;
			objWriter.Indentation = 1;
			objWriter.IndentChar = '\t';

			objWriter.WriteStartDocument();
			// <results>
			objWriter.WriteStartElement("results");

			var strPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "data");
			foreach (var strFile in Directory.GetFiles(strPath, "*.xml"))
			{
				var strPathReplace = strPath + Path.DirectorySeparatorChar;
				var strFileName = strFile.Replace(strPathReplace, string.Empty);

				// Do not bother to check custom files.
				if (!strFileName.StartsWith("custom") && !strFile.StartsWith("override") && !strFile.Contains("packs.xml") && !strFile.Contains("ranges.xml"))
				{
					// Load the current English file.
					var objEnglishDoc = new XmlDocument();
					objEnglishDoc = Load(strFile.Replace(strPathReplace, string.Empty));
					var objEnglishRoot = objEnglishDoc.SelectSingleNode("/chummer");

					// First pass: make sure the document exists.
					var blnExists = false;
					var objLanguageRoot = objLanguageDoc.SelectSingleNode("/chummer/chummer[@file = \"" + strFileName + "\"]");
					if (objLanguageRoot != null)
						blnExists = true;

					// <file name="x" exists="y">
					objWriter.WriteStartElement("file");
					objWriter.WriteAttributeString("name", strFileName);
					objWriter.WriteAttributeString("exists", blnExists.ToString());

					if (blnExists)
					{
						foreach (XmlNode objType in objEnglishRoot.ChildNodes)
						{
							objWriter.WriteStartElement(objType.Name);
							foreach (XmlNode objChild in objType.ChildNodes)
							{
								// If the Node has a source element, check it and see if it's in the list of books that were specified.
								// This is done since not all of the books are available in every language or the user may only wish to verify the content of certain books.
								var blnContinue = false;
								if (objChild["source"] != null)
								{
									foreach (var strBook in lstBooks)
									{
										if (strBook == objChild["source"].InnerText)
										{
											blnContinue = true;
											break;
										}
									}
								}
								else
									blnContinue = true;

								if (blnContinue)
								{
									if (objType.Name != "version" && !((objType.Name == "costs" || objType.Name == "safehousecosts") && strFile.EndsWith("lifestyles.xml")))
									{
										// Look for a matching entry in the Language file.
										if (objChild["name"] != null)
										{
											var objNode = objLanguageRoot.SelectSingleNode(objType.Name + "/" + objChild.Name + "[name = \"" + objChild["name"].InnerText + "\"]");
											if (objNode != null)
											{
												// A match was found, so see what elements, if any, are missing.
												var blnTranslate = false;
												var blnAltPage = false;
												var blnAdvantage = false;
												var blnDisadvantage = false;

												if (objChild.HasChildNodes)
												{
													if (objNode["translate"] != null)
														blnTranslate = true;

													// Do not mark page as missing if the original does not have it.
													if (objChild["page"] != null)
													{
														if (objNode["page"] != null)
															blnAltPage = true;
													}
													else
														blnAltPage = true;

													if (strFile.EndsWith("mentors.xml") || strFile.EndsWith("paragons.xml"))
													{
														if (objNode["advantage"] != null)
															blnAdvantage = true;
														if (objNode["disadvantage"] != null)
															blnDisadvantage = true;
													}
													else
													{
														blnAdvantage = true;
														blnDisadvantage = true;
													}
												}
												else
												{
													blnAltPage = true;
													if (objNode.Attributes["translate"] != null)
														blnTranslate = true;
												}

												// At least one pice of data was missing so write out the result node.
												if (!blnTranslate || !blnAltPage || !blnAdvantage || !blnDisadvantage)
												{
													// <results>
													objWriter.WriteStartElement(objChild.Name);
													objWriter.WriteAttributeString("exists", "True");
													objWriter.WriteElementString("name", objChild["name"].InnerText);
													if (!blnTranslate)
														objWriter.WriteElementString("missing", "translate");
													if (!blnAltPage)
														objWriter.WriteElementString("missing", "page");
													if (!blnAdvantage)
														objWriter.WriteElementString("missing", "advantage");
													if (!blnDisadvantage)
														objWriter.WriteElementString("missing", "disadvantage");
													// </results>
													objWriter.WriteEndElement();
												}
											}
											else
											{
												// No match was found, so write out that the data item is missing.
												// <result>
												objWriter.WriteStartElement(objChild.Name);
												objWriter.WriteAttributeString("exists", "False");
												objWriter.WriteElementString("name", objChild["name"].InnerText);
												// </result>
												objWriter.WriteEndElement();
											}

											if (strFileName == "metatypes.xml")
											{
												if (objChild["metavariants"] != null)
												{
													foreach (XmlNode objMetavariant in objChild.SelectNodes("metavariants/metavariant"))
													{
														var objTranslate = objLanguageRoot.SelectSingleNode("metatypes/metatype[name = \"" + objChild["name"].InnerText + "\"]/metavariants/metavariant[name = \"" + objMetavariant["name"].InnerText + "\"]");
														if (objTranslate != null)
														{
															var blnTranslate = false;
															var blnAltPage = false;

															if (objTranslate["translate"] != null)
																blnTranslate = true;
															if (objTranslate["page"] != null)
																blnAltPage = true;

															// Item exists, so make sure it has its translate attribute populated.
															if (!blnTranslate || !blnAltPage)
															{
																// <result>
																objWriter.WriteStartElement("metavariants");
																objWriter.WriteStartElement("metavariant");
																objWriter.WriteAttributeString("exists", "True");
																objWriter.WriteElementString("name", objMetavariant["name"].InnerText);
																if (!blnTranslate)
																	objWriter.WriteElementString("missing", "translate");
																if (!blnAltPage)
																	objWriter.WriteElementString("missing", "page");
																objWriter.WriteEndElement();
																// </result>
																objWriter.WriteEndElement();
															}
														}
														else
														{
															// <result>
															objWriter.WriteStartElement("metavariants");
															objWriter.WriteStartElement("metavariant");
															objWriter.WriteAttributeString("exists", "False");
															objWriter.WriteElementString("name", objMetavariant.InnerText);
															objWriter.WriteEndElement();
															// </result>
															objWriter.WriteEndElement();
														}
													}
												}
											}

											if (strFile == "martialarts.xml")
											{
												if (objChild["advantages"] != null)
												{
													foreach (XmlNode objAdvantage in objChild.SelectNodes("advantages/advantage"))
													{
														var objTranslate = objLanguageRoot.SelectSingleNode("martialarts/martialart[name = \"" + objChild["name"].InnerText + "\"]/advantages/advantage[. = \"" + objAdvantage.InnerText + "\"]");
														if (objTranslate != null)
														{
															// Item exists, so make sure it has its translate attribute populated.
															if (objTranslate.Attributes["translate"] == null)
															{
																// <result>
																objWriter.WriteStartElement("martialarts");
																objWriter.WriteStartElement("advantage");
																objWriter.WriteAttributeString("exists", "True");
																objWriter.WriteElementString("name", objAdvantage.InnerText);
																objWriter.WriteElementString("missing", "translate");
																objWriter.WriteEndElement();
																// </result>
																objWriter.WriteEndElement();
															}
														}
														else
														{
															// <result>
															objWriter.WriteStartElement("martialarts");
															objWriter.WriteStartElement("advantage");
															objWriter.WriteAttributeString("exists", "False");
															objWriter.WriteElementString("name", objAdvantage.InnerText);
															objWriter.WriteEndElement();
															// </result>
															objWriter.WriteEndElement();
														}
													}
												}
											}
										}
										else if (objChild.InnerText != null)
										{
											// The item does not have a name which means it should have a translate Attribute instead.
											var objNode = objLanguageRoot.SelectSingleNode(objType.Name + "/" + objChild.Name + "[. = \"" + objChild.InnerText + "\"]");
											if (objNode != null)
											{
												// Make sure the translate attribute is populated.
												if (objNode.Attributes["translate"] == null)
												{
													// <result>
													objWriter.WriteStartElement(objChild.Name);
													objWriter.WriteAttributeString("exists", "True");
													objWriter.WriteElementString("name", objChild.InnerText);
													objWriter.WriteElementString("missing", "translate");
													// </result>
													objWriter.WriteEndElement();
												}
											}
											else
											{
												// No match was found, so write out that the data item is missing.
												// <result>
												objWriter.WriteStartElement(objChild.Name);
												objWriter.WriteAttributeString("exists", "False");
												objWriter.WriteElementString("name", objChild.InnerText);
												// </result>
												objWriter.WriteEndElement();
											}
										}
									}
								}
							}
							objWriter.WriteEndElement();
						}

						// Now loop through the translation file and determine if there are any entries in there that are not part of the base content.
						foreach (XmlNode objType in objLanguageRoot.ChildNodes)
						{
							foreach (XmlNode objChild in objType.ChildNodes)
							{
								// Look for a matching entry in the English file.
								if (objChild["name"] != null)
								{
									var objNode = objEnglishRoot.SelectSingleNode("/chummer/" + objType.Name + "/" + objChild.Name + "[name = \"" + objChild["name"].InnerText + "\"]");
									if (objNode == null)
									{
										// <noentry>
										objWriter.WriteStartElement("noentry");
										objWriter.WriteStartElement(objChild.Name);
										objWriter.WriteElementString("name", objChild["name"].InnerText);
										objWriter.WriteEndElement();
										// </noentry>
										objWriter.WriteEndElement();
									}
								}
							}
						}
					}

					// </file>
					objWriter.WriteEndElement();
				}
			}

			// </results>
			objWriter.WriteEndElement();
			objWriter.WriteEndDocument();
			objWriter.Close();
			objStream.Close();
		}
		#endregion
	}
}
