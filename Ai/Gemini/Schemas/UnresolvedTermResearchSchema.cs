namespace FlowDesk.Ai.Gemini.Schemas;

public static class UnresolvedTermResearchSchema
{
    public static object Create()
    {
        return new
        {
            type = "object",

            additionalProperties = false,

            properties = new
            {
                terms = new
                {
                    type = "array",

                    description =
                        "Araştırılması istenen terimlerin sonuçları.",

                    maxItems = 10,

                    items = new
                    {
                        type = "object",

                        additionalProperties = false,

                        properties = new
                        {
                            term = new
                            {
                                type = "string",

                                description =
                                    "Kullanıcı tarafından verilen orijinal terim."
                            },

                            isResolved = new
                            {
                                type = "boolean",

                                description =
                                    "Terimin güvenilir web kaynaklarıyla " +
                                    "doğrulanıp doğrulanmadığı."
                            },

                            expandedForm = new
                            {
                                type = new[] { "string", "null" },

                                description =
                                    "Varsa terimin doğrulanmış açık yazımı. " +
                                    "Doğrulanamadıysa null."
                            },

                            turkishMeaning = new
                            {
                                type = new[] { "string", "null" },

                                description =
                                    "Varsa terimin güvenilir Türkçe karşılığı. " +
                                    "Doğrulanamadıysa null."
                            },

                            explanation = new
                            {
                                type = new[] { "string", "null" },

                                description =
                                    "Terimin bankacılık veya teknoloji " +
                                    "bağlamındaki kısa Türkçe açıklaması. " +
                                    "Doğrulanamadıysa null."
                            }
                        },

                        required = new[]
                        {
                            "term",
                            "isResolved",
                            "expandedForm",
                            "turkishMeaning",
                            "explanation"
                        }
                    }
                }
            },

            required = new[]
            {
                "terms"
            }
        };
    }
}