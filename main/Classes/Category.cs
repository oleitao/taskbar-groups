﻿using IWshRuntimeLibrary;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace client.Classes
{
    public class Category
    {
        public string Name;
        public string ColorString = System.Drawing.ColorTranslator.ToHtml(Color.FromArgb(31, 31, 31));
        public bool allowOpenAll = false;
        public List<ProgramShortcut> ShortcutList;
        public int Width; // not used aon
        public double Opacity = 10;
        Regex specialCharRegex = new Regex("[*'\",_&#^@]");

        public Category(string path)
        {
            // Use application's absolute path; (grabs the .exe)
            // Gets the parent folder of the exe and concats the rest of the path
            string fullPath;

            // Check if path is a full directory or part of a file name
            // Passed from the main shortcut client and the config client

            if (System.IO.File.Exists(@MainPath.path + @"\" + path + @"\ObjectData.xml"))
            {
                fullPath = @MainPath.path + @"\" + path + @"\ObjectData.xml";
            }
            else
            {
                fullPath = Path.GetFullPath(path + "\\ObjectData.xml");
            }

            System.Xml.Serialization.XmlSerializer reader =
                new System.Xml.Serialization.XmlSerializer(typeof(Category));
            using (StreamReader file = new StreamReader(fullPath))
            {
                Category category = (Category)reader.Deserialize(file);
                this.Name = category.Name;
                this.ShortcutList = category.ShortcutList;
                this.Width = category.Width;
                this.ColorString = category.ColorString;
                this.Opacity = category.Opacity;
                this.allowOpenAll = category.allowOpenAll;
            }
        }

        public Category() // needed for XML serialization
        {

        }

        public void CreateConfig(Image groupImage)
        {

            string path = @"config\" + this.Name;
            //string filePath = path + @"\" + this.Name + "Group.exe";
            //
            // Directory and .exe
            //
            System.IO.Directory.CreateDirectory(@path);

            //System.IO.File.Copy(@"config\config.exe", @filePath);
            //
            // XML config
            //
            System.Xml.Serialization.XmlSerializer writer =
                new System.Xml.Serialization.XmlSerializer(typeof(Category));

            using (FileStream file = System.IO.File.Create(@path + @"\ObjectData.xml"))
            {
                writer.Serialize(file, this);
                file.Close();
            }
            //
            // Create .ico
            //

            Image img = ImageFunctions.ResizeImage(groupImage, 1024, 1024); // Resize img if too big
            img.Save(path + @"\GroupImage.png");

            using (FileStream fs = new FileStream(path + @"\GroupIcon.ico", FileMode.Create))
            {
                ImageFunctions.IconFromImage(img).Save(fs);
                fs.Close();
            }
                                                            // saving as icon
                                                            //
                                                            // Create .lnk shortcut
                                                            //


            // Through shellLink.cs class, pass through into the function information on how to construct the icon
            // Needed due to needing to set a unique AppUserModelID so the shortcut applications don't stack on the taskbar with the main application
            // Tricks Windows to think they are from different applications even though they are from the same .exe
            ShellLink.InstallShortcut(
                Path.GetFullPath(@System.AppDomain.CurrentDomain.FriendlyName),
                "tjackenpacken.taskbarGroup.menu." + this.Name,
                 path + " shortcut",
                 Path.GetFullPath(@path),
                 Path.GetFullPath(path + @"\GroupIcon.ico"),
                 path + "\\" + this.Name + ".lnk",
                 this.Name
            );


            // Build the icon cache
            cacheIcons();

            System.IO.File.Move(@path + "\\" + this.Name + ".lnk",
                Path.GetFullPath(@"Shortcuts\" + Regex.Replace(this.Name, @"(_)+", " ") + ".lnk")); // Move .lnk to correct directory
        }

        public Bitmap LoadIconImage() // Needed to access img without occupying read/write
        {
            string path = @"config\" + Name + @"\GroupImage.png";

            using (MemoryStream ms = new MemoryStream(System.IO.File.ReadAllBytes(path)))
                return new Bitmap(ms);
        }

        // Goal is to create a folder with icons of the programs pre-cached and ready to be read
        // Avoids having the icons need to be rebuilt everytime which takes time and resources
        public void cacheIcons()
        {

            // Defines the paths for the icons folder
            string path = @MainPath.path + @"\config\" + this.Name;
            string iconPath = path + "\\Icons\\";

            // Check and delete current icons folder to completely rebuild the icon cache
            // Only done on re-edits of the group and isn't done usually
            if (Directory.Exists(iconPath))
            {
                Directory.Delete(iconPath, true);
            }

            // Creates the icons folder inside of existing config folder for the group
            Directory.CreateDirectory(iconPath);

            iconPath = @path + @"\Icons\";

            // Loops through each shortcut added by the user and gets the icon
            // Writes the icon to the new folder in a .jpg format
            // Namign scheme for the files are done through Path.GetFileNameWithoutExtension()
            for (int i = 0; i < ShortcutList.Count; i++)
            {
                String filePath = ShortcutList[i].FilePath;

                // Process .lnk (shortcut) files differently
                if (ShortcutList[i].isWindowsApp)
                {
                    handleWindowsApp.getWindowsAppIcon(filePath, true).Save(iconPath + "\\" + specialCharRegex.Replace(filePath, string.Empty) + ".jpg");
                }
                else if (Path.GetExtension(filePath).ToLower() == ".lnk")
                {
                    Forms.frmGroup.handleLnkExt(filePath).Save(iconPath + "\\" + Path.GetFileNameWithoutExtension(filePath) + ".jpg");
                } else if (Directory.Exists(filePath))
                {
                    handleFolder.GetFolderIcon(filePath).ToBitmap().Save(iconPath + "\\" + Path.GetFileNameWithoutExtension(filePath) + "_FolderObjTSKGRoup.jpg");
                } else
                {
                    // Extracts icon from the .exe if the provided file is not a shortcut file
                    Icon.ExtractAssociatedIcon(Environment.ExpandEnvironmentVariables(filePath)).ToBitmap().Save(iconPath + "\\" + Path.GetFileNameWithoutExtension(filePath) + ".jpg");
                }
            }
        }

        // Try to load an iamge from the cache
        // Takes in a programPath (shortcut) and processes it to the proper file name
        public Image loadImageCache(ProgramShortcut shortcutObject)
        {

            String programPath = shortcutObject.FilePath;

            if (System.IO.File.Exists(programPath) || Directory.Exists(programPath) || shortcutObject.isWindowsApp)
            {
                try
                {
                    // Try to construct the path like if it existed
                    // If it does, directly load it into memory and return it
                    // If not then it would throw an exception in which the below code would catch it
                    String cacheImagePath = @Path.GetDirectoryName(Application.ExecutablePath) + @"\config\" + this.Name + @"\Icons\" + ((shortcutObject.isWindowsApp) ? specialCharRegex.Replace(programPath, string.Empty) : @Path.GetFileNameWithoutExtension(programPath)) + (Directory.Exists(programPath)? "_FolderObjTSKGRoup.jpg" : ".jpg");

                    using (MemoryStream ms = new MemoryStream(System.IO.File.ReadAllBytes(cacheImagePath)))
                        return Image.FromStream(ms);
                    
                }
                catch (Exception)
                {
                    // Try to recreate the cache icon image and catch and missing file/icon situations that may arise

                    // Checks if the original file even exists to make sure to not do any extra operations

                    // Same processing as above in cacheIcons()
                    String path = MainPath.path + @"\config\" + this.Name + @"\Icons\" + Path.GetFileNameWithoutExtension(programPath) + (Directory.Exists(programPath) ? "_FolderObjTSKGRoup.jpg" : ".jpg");

                    Image finalImage;

                    if (Path.GetExtension(programPath).ToLower() == ".lnk")
                    {
                        finalImage = Forms.frmGroup.handleLnkExt(programPath);
                    }
                    else if (Directory.Exists(programPath))
                    {
                        finalImage = handleFolder.GetFolderIcon(programPath).ToBitmap();
                    } else 
                    {
                        finalImage = Icon.ExtractAssociatedIcon(programPath).ToBitmap();
                    }


                    // Above all sets finalIamge to the bitmap that was generated from the icons
                    // Save the icon after it has been fetched by previous code
                    finalImage.Save(path);

                    // Return the said image
                    return finalImage;
                }
            }
            else
            {
                return global::client.Properties.Resources.Error;
            }
        }
        //
        // END OF CLASS
        //
    }
}
