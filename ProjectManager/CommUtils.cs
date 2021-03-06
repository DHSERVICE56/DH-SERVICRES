
using Scada.Data.Configuration;
using Scada.Data.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;

namespace Scada.Comm
{

    public static partial class CommUtils
    {
      
        public enum ProtocolLogFormats
        {
            
            Hex,
            
            String
        }

        
        public const string AppVersion = "5.1.0.0";

        /// <summary>
        /// Формат даты и времени для вывода в журнал линии связи
        /// </summary>
        public const string CommLineDTFormat = "yyyy'-'MM'-'dd' 'HH':'mm':'ss";
        
        /// <summary>
        /// Время актуальности команды управления, с
        /// </summary>
        private const int CmdLifeTime = 60;

        /// <summary>
        /// Максимальный номер имени файла команды управления
        /// </summary>
        private const int MaxCmdFileNum = 999;

        private static int cmdFileNum = 1;            // текущий номер имени файла команды управления
        private static object cmdLock = new object(); // объект для синхронизации записи команд управления


        /// <summary>
        /// Преобразовать массив байт в строку 16-ричных чисел, разделённых пробелами
        /// </summary>
        public static string BytesToHex(byte[] buffer, int index, int length)
        {
            StringBuilder sbResult = new StringBuilder();

            for (int i = 0; i < length; i++)
            {
                if (i > 0)
                    sbResult.Append(" ");
                sbResult.Append(buffer[index + i].ToString("X2"));
            }

            return sbResult.ToString();
        }

        /// <summary>
        /// Преобразовать массив байт в строку 16-ричных чисел, разделённых пробелами
        /// </summary>
        public static string BytesToHex(byte[] buffer)
        {
            return BytesToHex(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Преобразовать массив байт в текстовую строку
        /// </summary>
        public static string BytesToString(byte[] buffer, int index, int length, bool skipNonPrinting = false)
        {
            StringBuilder sbResult = new StringBuilder();
            bool notSkip = !skipNonPrinting;
            int n = index + length;

            for (int i = index; i < n; i++)
            {
                byte b = buffer[i];

                if (b >= 32)
                {
                    sbResult.Append(Encoding.Default.GetString(buffer, i, 1));
                }
                else if (notSkip)
                {
                    sbResult.Append("<");
                    sbResult.Append(b.ToString("X2"));
                    sbResult.Append(">");
                }
            }

            return sbResult.ToString();
        }

        /// <summary>
        /// Преобразовать массив байт в текстовую строку
        /// </summary>
        public static string BytesToString(byte[] buffer)
        {
            return BytesToString(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Добавить к числу value спереди нули до достжения длины length
        /// </summary>
        public static string AddZeros(int value, int length)
        {
            StringBuilder sbResult = new StringBuilder(value.ToString());
            while (sbResult.Length < length)
                sbResult.Insert(0, "0");
            return sbResult.ToString();
        }

        /// <summary>
        /// Извлечь хост и порт из позывного КП
        /// </summary>
        public static void ExtractHostAndPort(string callNum, int defaultPort, out string host, out int port)
        {
            string portStr;
            int ind = callNum.IndexOf(':');

            if (ind >= 0)
            {
                host = callNum.Substring(0, ind);
                portStr = callNum.Substring(ind + 1);
            }
            else
            {
                host = callNum;
                portStr = "";
            }

            if (portStr == "")
            {
                port = defaultPort;
            }
            else if (!(int.TryParse(portStr, out port) && port > 0))
            {
                throw new FormatException(Localization.UseRussian ?
                    "Некорректный порт." :
                    "Port is incorrect.");
            }
        }

        /// <summary>
        /// Извлечь IP-адрес и порт из позывного КП
        /// </summary>
        public static void ExtractAddrAndPort(string callNum, int defaultPort, out IPAddress addr, out int port)
        {
            string host;
            ExtractHostAndPort(callNum, defaultPort, out host, out port);

            if (!IPAddress.TryParse(host, out addr))
            {
                throw new FormatException(Localization.UseRussian ?
                    "Некорректный IP-адрес." :
                    "IP address is incorrect.");
            }
        }

        /// <summary>
        /// Получить текущую дату и время для вывода в журнал линии связи
        /// </summary>
        public static string GetNowDT()
        {
            return DateTime.Now.ToString(CommLineDTFormat);
        }


        /// <summary>
        /// Получить строковое значение параметра
        /// </summary>
        public static string GetStringParam(this SortedList<string, string> paramList,
            string name, bool required, string defaultValue)
        {
            string val;
            if (paramList.TryGetValue(name, out val))
                return val;
            else if (required)
                throw new ArgumentException(string.Format(CommPhrases.ParamRequired, name));
            else
                return defaultValue;
        }

        /// <summary>
        /// Получить логическое значение параметра
        /// </summary>
        public static bool GetBoolParam(this SortedList<string, string> paramList,
            string name, bool required, bool defaultValue)
        {
            string valStr;
            bool val;

            if (paramList.TryGetValue(name, out valStr))
            {
                if (bool.TryParse(valStr, out val))
                {
                    return val;
                }
                else if (required)
                {
                    throw new FormatException(string.Format(CommPhrases.IncorrectParamVal, name));
                }
                else
                {
                    return defaultValue;
                }
            }
            else if (required)
            {
                throw new ArgumentException(string.Format(CommPhrases.ParamRequired, name));
            }
            else
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Получить целочисленное значение параметра
        /// </summary>
        public static int GetIntParam(this SortedList<string, string> paramList,
            string name, bool required, int defaultValue)
        {
            string valStr;
            int val;

            if (paramList.TryGetValue(name, out valStr))
            {
                if (int.TryParse(valStr, out val))
                {
                    return val;
                }
                else if (required)
                {
                    throw new FormatException(string.Format(CommPhrases.IncorrectParamVal, name));
                }
                else
                {
                    return defaultValue;
                }
            }
            else if (required)
            {
                throw new ArgumentException(string.Format(CommPhrases.ParamRequired, name));
            }
            else
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Получить значение параметра перечислимого типа
        /// </summary>
        public static T GetEnumParam<T>(this SortedList<string, string> paramList,
            string name, bool required, T defaultValue) where T : struct
        {
            string valStr;
            T val;

            if (paramList.TryGetValue(name, out valStr))
            {
                if (Enum.TryParse<T>(valStr, true, out val))
                {
                    return val;
                }
                else if (required)
                {
                    throw new FormatException(string.Format(CommPhrases.IncorrectParamVal, name));
                }
                else
                {
                    return defaultValue;
                }
            }
            else if (required)
            {
                throw new ArgumentException(string.Format(CommPhrases.ParamRequired, name));
            }
            else
            {
                return defaultValue;
            }
        }


        /// <summary>
        /// Записать команду службе SCADA-Коммуникатора в файл
        /// </summary>
        public static bool SaveCmd(string cmdDir, string sender, string cmdType, string[] cmdParams, out string msg)
        {
            try
            {
                if (cmdDir == "")
                {
                    msg = Localization.UseRussian ?
                        "Невозможно записать команду, т.к. директория команд неопределена." :
                        "Unable to write a command because the command directory is undefined.";
                    return false;
                }

                // формирование содержимого файла команды
                StringBuilder sbCmd = new StringBuilder();
                sbCmd
                    .AppendLine("[Command]")
                    .AppendLine("Target=ScadaCommSvc")
                    .Append("Sender=").AppendLine(sender)
                    .Append("User=").AppendLine(Environment.UserName)
                    .Append("DateTime=").AppendLine(DateTime.Now.ToString(DateTimeFormatInfo.InvariantInfo))
                    .Append("LifeTime=").AppendLine(CmdLifeTime.ToString())
                    .Append("CmdType=").AppendLine(cmdType);
                foreach (string param in cmdParams)
                    sbCmd.AppendLine(param);
                sbCmd.AppendLine("End=");

                // формирование имени файла команды
                string fileName = ""; // имя файла команды

                lock (cmdLock)
                {
                    int num = cmdFileNum;

                    do
                    {
                        string name = cmdDir + "cmd" + AddZeros(num, 3) + ".dat";
                        if (!File.Exists(name))
                            fileName = name;
                        else if (++num > MaxCmdFileNum)
                            num = 1;
                    } while (fileName == "" && num != cmdFileNum);

                    cmdFileNum = num < MaxCmdFileNum ? num + 1 : 1;
                }

                if (fileName == "")
                {
                    msg = Localization.UseRussian ?
                        "Невозможно записать команду, т.к. отсутствуют доступные имена файлов команд." :
                        "Unable to write a command because available command file names are missing.";
                    return false;
                }

                // запись команды
                using (FileStream fileStream = 
                    new FileStream(fileName, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite))
                {
                    using (StreamWriter streamWriter = new StreamWriter(fileStream, Encoding.Default))
                    {
                        streamWriter.Write(sbCmd.ToString());
                    }
                }

                msg = Localization.UseRussian ?
                    "Команда отправлена успешно." : 
                    "The command has been sent successfully";
                return true;
            }
            catch (Exception ex)
            {
                msg = (Localization.UseRussian ?
                    "Ошибка при записи команды: " : 
                    "Error saving command: ") + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Записать команду КП в файл
        /// </summary>
        public static bool SaveCmd(string cmdDir, string sender, Command cmd, out string msg)
        {
            string[] cmdParams = new string[] {
                "KPNum=" + cmd.KPNum,
                "CmdNum=" + cmd.CmdNum,
                "CmdVal=" + cmd.CmdVal.ToString(),
                "CmdData=" + (cmd.CmdData == null ? "" : ScadaUtils.BytesToHex(cmd.CmdData))
            };

            return SaveCmd(cmdDir, sender, BaseValues.CmdTypes.GetCmdTypeCode(cmd.CmdTypeID), cmdParams, out msg);
        }
    }
}
