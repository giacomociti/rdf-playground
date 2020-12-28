open System.IO
open VDS.RDF
open Utils
open RdfWorkflow

let runFromFiles (configuration: FileInfo) (workflow: FileInfo) (input: FileInfo) =
    let textResolver x = Path.Combine(workflow.Directory.FullName, x) |> File.ReadAllText
    let steps = Steps([], textResolver)
    let configuration = parseTurtleFile configuration.FullName
    let workflow = parseTurtleFile workflow.FullName
    let input = parseTurtleFile input.FullName
    Workflow(configuration, steps, workflow).Start input

[<EntryPoint>]
let main argv =
    match argv with
    | [| configuration; workflow; input |] ->
        let c, w, i = (FileInfo configuration), (FileInfo workflow), (FileInfo input)

        let res = runFromFiles c w i

        printfn $"{res.Status} <{res.StepUri}> after {res.StepNumber} steps" 
        let resultName = $"{Path.GetFileNameWithoutExtension(i.Name)}.out{i.Extension}"
        let resultFile = Path.Combine(i.Directory.FullName, resultName)
        res.Data.SaveToFile resultFile
        0
    | _ -> 
        printfn "invalid arguments"
        1
