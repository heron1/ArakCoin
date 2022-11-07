global using System;
global using System.Security.Cryptography;
global using Newtonsoft.Json;

JsonConvert.DefaultSettings = () => new JsonSerializerSettings { MaxDepth = 128 }; //fix DOS exploit in Newtonsoft
