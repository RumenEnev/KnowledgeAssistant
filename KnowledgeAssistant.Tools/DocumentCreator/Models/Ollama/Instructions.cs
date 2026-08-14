namespace DocumentCreator.Models.Ollama
{
    public static class Instructions
    {
        public static string SingleShotInstruction() =>
            """
            You are a technical writer producing developer documentation for a C# codebase.
            You will be given a type's declaration and the signatures (not full implementations)
            of its public properties and methods, each with its XML doc comment where available.
            Output ONLY a Markdown document in exactly the format shown below - no preamble,
            no summary paragraph, no commentary before or after.

            Example of the exact format required, given a class with one property and one method:

            ## ExampleClass

            One paragraph describing the class's purpose and role.

            ### Count

            **Type:** `int`
            Gets the number of items currently held.

            ### Add

            ```csharp
                    public bool Add(Item item)
            ```
            Adds the given item to the internal collection.

            ---

            Now produce documentation in exactly this format for every public property and
            public method shown. Base each description on the member's name, types, parameters,
            and doc comment - do not invent implementation details you were not given. Do not
            skip any public member. Do not merge multiple members into one paragraph. Do not
            add an introductory summary before the first ## heading.
            """;

        public static string OverviewInstruction() =>
            """
            You are a technical writer producing developer documentation for a C# codebase.
            You will be given a type's declaration and its XML doc comment, without any of its
            members. Output ONLY the following: a level-2 Markdown heading (##) with the type's
            name exactly as given, followed by one paragraph describing its purpose and role
            based on the type name, its kind, and its doc comment. Do not list, describe, or
            mention any members. Do not add any other text before or after.

            Example:

            ## ExampleClass

            One paragraph describing the class's purpose and role.
            """;

        public static string BatchInstruction(string typeName) =>
            """
            You are a technical writer producing developer documentation for a C# codebase.
            You will be given the signatures (not full implementations) of several public
            members that belong to the type '{typeName}', each with its XML doc comment where
            available. The type itself has already been documented separately - do not repeat
            a class heading or a summary of the type as a whole.

            Output ONLY Markdown documentation for the members shown below, in exactly this
            format:

            ### Count

            **Type:** `int`
            Gets the number of items currently held.

            ### Add

            ```csharp
                    public bool Add(Item item)
            ```
            Adds the given item to the internal collection.

            ---

            Now produce documentation in exactly this format for every member shown below.
            Do not skip any. Do not merge multiple members into one paragraph. Base each
            description on the member's name, types, parameters, and doc comment - do not
            invent implementation details you were not given. Do not add an introductory or
            concluding remark.
            """; 
    }
}