using DumaCommon.Engine.Tilemap;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Forms;

namespace LendaMapConverter
{
    class Program
    {
        private static string filePath;
        private static string fileName;
        private static string fileExtension;

        private static byte[] xnbMapHeader = {
            0x58, 0x4E, 0x42, 0x77, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x30,
            0x44, 0x75, 0x6D, 0x61, 0x43, 0x6F, 0x6D, 0x6D, 0x6F, 0x6E, 0x2E, 0x4D,
            0x61, 0x70, 0x54, 0x65, 0x6D, 0x70, 0x6C, 0x61, 0x74, 0x65, 0x44, 0x61,
            0x74, 0x61, 0x54, 0x79, 0x70, 0x65, 0x52, 0x65, 0x61, 0x64, 0x65, 0x72,
            0x2C, 0x20, 0x44, 0x75, 0x6D, 0x61, 0x43, 0x6F, 0x6D, 0x6D, 0x6F, 0x6E,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x01
        };

        [STAThread]
        public static void Main(string[] args)
        {
            string dllNotFoundMessage = !File.Exists("./DumaCommon.dll") ? "\n- DumaCommon.dll" : "";
            dllNotFoundMessage += !File.Exists("./MonoGame.Framework.dll") ? "\n- MonoGame.Framework.dll" : "";
            dllNotFoundMessage += !File.Exists("./Newtonsoft.Json.dll") ? "\n- Newtonsoft.Json.dll" : "";

            if (!String.IsNullOrEmpty(dllNotFoundMessage))
            {
                handleException(new Exception("Você precisa adicionar as dlls no diretório do programa:" + dllNotFoundMessage));
                Console.Write("Pressione qualquer tecla para finalizar...");
                Console.ReadKey();
                return;
            }

            var dialog = new OpenFileDialog
            {
                Multiselect = false,
                Title = "Open XNB or JSON file",
                Filter = "XNB or JSON file|*.xnb;*.json"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                filePath = Path.GetDirectoryName(dialog.FileName);
                fileName = Path.GetFileNameWithoutExtension(dialog.FileName);
                fileExtension = Path.GetExtension(dialog.FileName);

                if (fileExtension == ".xnb")
                {
                    try
                    {
                        unpack();
                    }
                    catch (Exception e)
                    {
                        handleException(e);
                    }
                }
                else if (fileExtension == ".json")
                {
                    try
                    {
                        pack();
                    }
                    catch (Exception e)
                    {
                        handleException(e);
                    }
                }

                Console.Write("Pressione qualquer tecla para finalizar...");
                Console.ReadKey();
            }
        }

        static void handleException(Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Erro: {e.Message}");
            Console.ResetColor();
        }

        static void pack()
        {
            // Abre o arquivo XNB
            Console.WriteLine($"[Log] Diretório: {filePath}");
            Console.WriteLine($"[Log] Abrindo arquivo {fileName + fileExtension}");
            string input = File.ReadAllText(Path.Combine(filePath, fileName + fileExtension));

            // Deserializa o json
            Console.WriteLine($"[Log] Deserializando json");
            MapTemplate map = JsonConvert.DeserializeObject<MapTemplate>(input);

            // Serializa com binary formatter
            Console.WriteLine($"[Log] Serializando objeto");
            MemoryStream mapSerialized = new MemoryStream();
            BinaryFormatter binaryFormatter = new BinaryFormatter();
            binaryFormatter.Serialize(mapSerialized, map);

            // Junta o cabeçalho padrão com o conteúdo serializado
            Console.WriteLine($"[Log] Montando arquivo");
            byte[] result = new byte[mapSerialized.Length + xnbMapHeader.Length];
            xnbMapHeader.CopyTo(result, 0);
            mapSerialized.ToArray().CopyTo(result, xnbMapHeader.Length);

            // Salva o arquivo
            Console.WriteLine($"[Log] Salvando arquivo {fileName}.xnb");
            File.WriteAllBytes(Path.Combine(filePath, fileName + ".xnb"), result);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[Log] Sucesso! {fileName + fileExtension} -> {fileName}.xnb");
            Console.ResetColor();
        }

        static void unpack()
        {
            // Abre o arquivo XNB
            Console.WriteLine($"[Log] Diretório: {filePath}");
            Console.WriteLine($"[Log] Abrindo arquivo {fileName + fileExtension}");
            FileStream input = File.Open(Path.Combine(filePath, fileName + fileExtension), FileMode.Open);

            // Verifica se existe "DumaCommon.MapTemplateDataTypeReader" no cabeçalho
            Console.WriteLine($"[Log] Validando arquivo");
            byte[] header = new byte[66];
            input.Read(header, 0, header.Length);
            string headerString = System.Text.Encoding.ASCII.GetString(header);

            if (!headerString.Contains("DumaCommon.MapTemplateDataTypeReader"))
            {
                throw new Exception("O arquivo selecionado não é do tipo esperado.");
            }

            // Pula os bytes do cabeçalho
            input.Position = 66;

            // Cria um array de bytes para guardar o conteúdo do xnb
            Console.WriteLine($"[Log] Salvando conteúdo");
            byte[] content = new byte[input.Length - (long)Convert.ToDecimal(66)];
            input.Read(content, 0, content.Length);

            // Cria um memorystream do conteúdo para ser deserializado
            MemoryStream contentStream = new MemoryStream(content);

            // Deserializa o conteúdo
            Console.WriteLine("[Log] Deserializando conteúdo");
            BinaryFormatter binaryFormatter = new BinaryFormatter();
            MapTemplate mapTemplate = (MapTemplate)binaryFormatter.Deserialize(contentStream);

            // Converte o MapTemplate para TiledMapTemplate (que possui 2 novas propriedades nas layers que o tiled precisa)
            Console.WriteLine("[Log] Convertendo objeto");
            TiledMapTemplate map = JsonConvert.DeserializeObject<TiledMapTemplate>(JsonConvert.SerializeObject(mapTemplate));

            // Serializa para json e salva o arquivo
            Console.WriteLine($"[Log] Salvando arquivo {fileName}.json");
            string json = JsonConvert.SerializeObject(map);
            File.WriteAllText(Path.Combine(filePath, fileName + ".json"), json);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[Log] Sucesso! {fileName + fileExtension} -> {fileName}.json");
            Console.ResetColor();
        }
    }
}
