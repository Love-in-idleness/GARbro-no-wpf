using System;
using GameRes;
using Newtonsoft.Json;

namespace GARbro.Application;

/// <summary>Keys belong to the selected provider, not to every nested container.</summary>
public sealed class ScopedOptionsProvider : IOptionsProvider
{
    readonly DecoderContext context;
    readonly JsonOptionsProvider fallback;
    readonly string formatId;
    public ScopedOptionsProvider(WorkerRequest request)
    {
        context = request.Context ?? new(); formatId = request.OptionsFormatId;
        fallback = new JsonOptionsProvider(request.OptionsJson, request.Scheme);
    }
    public ResourceOptions GetOptions(IResource format, string notice)
    {
        string id = FormatIdentity.Of(format);
        try
        {
            if (context.OptionsByFormat?.TryGetValue(id, out string json) == true)
                return new JsonOptionsProvider(json).GetOptions(format, notice);
            if (formatId == null || formatId == id) return fallback.GetOptions(format, notice);
            return new JsonOptionsProvider().GetOptions(format, notice);
        }
        catch (JsonException error)
        {
            throw new MissingOptionsException(format.Tag + ": " + error.Message)
            { FormatId = id, OptionsJson = JsonConvert.SerializeObject(format.GetDefaultOptions(), Formatting.Indented) };
        }
    }
}
