using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace ConfigParser
{
    /// <summary>
    /// This Class is used to modify ini style file.
    /// </summary>
    class IniReader
    {
        /// <summary>
        /// Store the ini filepath.(Can be both relative or absolute)
        /// </summary>
        string iniFilePath;
        /// <summary>
        /// The text to return if the key-value pair does not exists.
        /// </summary>
        string Notext;

        /// <summary>
        /// Initialize the object using ini file path.(Can be both relative and relative)
        /// </summary>
        /// <param name="filepath"></param>
        public IniReader(string filepath)
        {
            iniFilePath = filepath;
            Notext = string.Empty;
        }

        //This region is to call a local API with the function to direct read and write ini file using the style of key-value pair.
        #region API Function Clarification
        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);
        [DllImport("kernel32")]
        private static extern long GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);
        #endregion
        

        /// <summary>
        /// This is used to read value according to key and value.
        /// </summary>
        /// <param name="section"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public string Read(string section,string key)
        {
            if (File.Exists(iniFilePath))
            {
                StringBuilder tmp = new StringBuilder(1024);
                try
                {
                    GetPrivateProfileString(section, key, Notext, tmp, 1024, iniFilePath);
                    return tmp.ToString();
                }
                catch(Exception e)
                {
                    throw e;
                }
            }
            else
                throw new Exception("Config File not found");
        }

        /// <summary>
        /// This is used to write new key-value pair or modify existing key-value pair in ini file.Return valu indicates success of failure.
        /// </summary>
        /// <param name="section"></param>
        /// Name is in "[]" in ini file.
        /// <param name="key"></param>
        /// Key is before "=" in ini file.
        /// <param name="value"></param>
        /// Value is after "=" in ini file.
        /// <returns></returns>
        public bool Write(string section,string key,string value)
        {
            if (File.Exists(iniFilePath))
            {
                var opResult = WritePrivateProfileString(section, key, value, iniFilePath);
                if (opResult == 0)
                    return false;
                else
                    return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Set Notext property in the class.
        /// </summary>
        /// <param name="text"></param>
        public void setNoText(string text)
        {
            Notext = text;
        }

    }
}
