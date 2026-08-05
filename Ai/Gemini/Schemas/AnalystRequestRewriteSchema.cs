namespace FlowDesk.Ai.Gemini.Schemas;

public static class AnalystRequestRewriteSchema
{
    public static object Create()
    {
        return new
        {
            type = "object",

            additionalProperties = false,

            properties = new
            {
                rewrittenRequest = new
                {
                    type = "string",

                    description =
                        "Orijinal talebin anlamı değiştirilmeden, " +
                        "analistin anlayacağı açık ve profesyonel Türkçe metin."
                },

                abbreviations = new
                {
                    type = "array",

                    description =
                        "Anlamından emin olunan kısaltmaların açıklamaları.",

                    maxItems = 20,

                    items = new
                    {
                        type = "object",

                        additionalProperties = false,

                        properties = new
                        {
                            abbreviation = new
                            {
                                type = "string",

                                description =
                                    "Orijinal talepte geçen kısaltma."
                            },

                            expandedForm = new
                            {
                                type = "string",

                                description =
                                    "Kısaltmanın açık yazımı."
                            },

                            explanation = new
                            {
                                type = new[] { "string", "null" },

                                description =
                                    "Kısaltmanın talep bağlamındaki kısa açıklaması. " +
                                    "Ek açıklama gerekmiyorsa null."
                            }
                        },

                        required = new[]
                        {
                            "abbreviation",
                            "expandedForm",
                            "explanation"
                        }
                    }
                },

                ambiguities = new
                {
                    type = "array",

                    description =
                        "Talepte eksik, çelişkili veya birden fazla şekilde " +
                        "yorumlanabilecek noktalar.",

                    maxItems = 20,

                    items = new
                    {
                        type = "string"
                    }
                },

                unresolvedTerms = new
                {
                    type = "array",

                    description =
                        "Anlamından emin olunmayan ve bu nedenle açıklanmayan " +
                        "kurum içi terimler veya kısaltmalar.",

                    maxItems = 20,

                    items = new
                    {
                        type = "string"
                    }
                }
            },

            required = new[]
            {
                "rewrittenRequest",
                "abbreviations",
                "ambiguities",
                "unresolvedTerms"
            }
        };
    }
}