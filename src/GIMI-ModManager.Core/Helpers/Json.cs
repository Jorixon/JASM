﻿using Newtonsoft.Json;

namespace GIMI_ModManager.Core.Helpers;

public static class Json
{
    public static async Task<T> ToObjectAsync<T>(string value)
    {
        return await Task.Run(() => JsonConvert.DeserializeObject<T>(value));
    }

    public static async Task<string> StringifyAsync(object value)
    {
        return await Task.Run(() => JsonConvert.SerializeObject(value, Formatting.Indented));
    }
}