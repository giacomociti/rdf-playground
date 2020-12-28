namespace RdfWorkflow

// TODO
// use InMemoryManager to keeep separate graphs for input, config, temp, output...
// plug additional services (persistent resume token, tracing..)

open System
open Iride
open Utils
open VDS.RDF
open VDS.RDF.Update
open VDS.RDF.Writing
open VDS.RDF.Writing.Formatting
open VDS.RDF.Shacl
open System.Collections.Generic
open VDS.RDF.Query

type Schema = GraphProvider<Schema = "schema.ttl">
type Classes = UriProvider<"schema.ttl", SchemaQuery.RdfsClasses>

type Steps (customActions, textResolver) =

    let askStep node =
        let step = Schema.AskStep node
        let query = step.SparqlQuery.Single |> textResolver |> parseQuery
        fun data ->
            if ask query data
            then step.NextOnTrue.Single.Resource
            else step.NextOnFalse.Single.Resource

    let shaclStep node =
        let step = Schema.ShaclStep node
        let shapes = new ShapesGraph(step.Shapes.Single |> textResolver |> parseTurtleText)
        fun data ->
            let result = shacl shapes data
            if result.Conforms
            then step.NextOnValid.Single.Resource
            else
                data.Merge result.Graph
                step.NextOnInvalid.Single.Resource

    let constructStep node =
        let step = Schema.ConstructStep node
        let query = step.SparqlQuery.Single |> textResolver |> parseQuery
        fun data ->
            let result = construct query data
            data.Merge result
            step.Next.Single.Resource

    let rdfsInferenceStep node =
        let step = Schema.RdfsInferenceStep node
        let schemas = step.Schema |> Seq.map (textResolver >> parseTurtleText)
        fun data ->
            infer schemas data
            step.Next.Single.Resource

    let remoteUpdateGraphStep node =
        let step = Schema.RemoteUpdateGraphStep node
        let endpointUri = step.RemoteEndpoint.Single.Endpoint.Single.Uri
        let endpoint = SparqlRemoteUpdateEndpoint(endpointUri)
        let versionUri = step.VersionProperty.Single.Uri
        fun (data: IGraph) ->
            let t = data.GetTriplesWithPredicate(versionUri).Single
            let graphUri = t.Subject.Uri
            let version = NTriplesFormatter().Format(t.Object)
            let sw = new IO.StringWriter()
            sw.WriteLine $"WITH <{graphUri}>"
            sw.WriteLine "DELETE { ?s ?p ?o }"
            sw.WriteLine "INSERT {"
            NTriplesWriter().Save(data, sw, leaveOpen=true)
            sw.WriteLine "}"
            sw.WriteLine "WHERE {"
            sw.WriteLine $" OPTIONAL {{ <{graphUri}> <{versionUri}> ?v }}"
            sw.WriteLine " BIND(COALESCE(?v, 0) AS ?version)"
            sw.WriteLine $" FILTER (?version < {version})"
            sw.WriteLine " OPTIONAL { ?s ?p ?o }"
            sw.WriteLine "}"
            let command = sw.ToString()
            endpoint.Update command
            step.Next.Single.Resource

    let remoteUpdateStep node =
        let step = Schema.RemoteUpdateStep node
        let endpointUri = step.RemoteEndpoint.Single.Endpoint.Single.Uri
        let endpoint = SparqlRemoteUpdateEndpoint(endpointUri)
        let argsQuery = step.ArgumentsQuery.Single |> textResolver |> parseQuery
        let update = step.SparqlUpdate.Single |> textResolver |> SparqlParameterizedString
        fun data ->
            let args = select argsQuery data
            let command = getCommands update args |> String.concat ";\n"
            if command.Length > 0
            then endpoint.Update command
            step.Next.Single.Resource

    let coreSteps = [
        Classes.AskStep, askStep
        Classes.ConstructStep, constructStep
        Classes.ShaclStep, shaclStep
        Classes.RdfsInferenceStep, rdfsInferenceStep
        Classes.RemoteUpdateGraphStep, remoteUpdateGraphStep
        Classes.RemoteUpdateStep, remoteUpdateStep ]

    let adapt action node data =
        action data
        (Schema.Step node).Next.Single.Resource

    let customSteps = 
        customActions 
        |> List.map (fun (uri, action) -> uri, adapt action)

    let allSteps = coreSteps @ customSteps |> dict

    let getStepType step =
        step.Node.Types
        |> Seq.filter (fun t -> t <> Classes.Step)
        |> Seq.exactlyOne

    let preparedSteps = Dictionary<_, _>()

    member _.Get(step: Resource) =
        match preparedSteps.TryGetValue step.Uri with
        | true, result -> result
        | _ -> 
            match allSteps.TryGetValue (getStepType step) with
            | true, newStep -> 
                let result = newStep step
                preparedSteps.Add(step.Node.Uri, result)
                result
            | _ -> failwith $"Unknown step {step}"


type State = { StepNumber: int; Step: Resource; Result: bool option }
type Status = Succeded | Failed | Suspended
type Response = { StepNumber: int; StepUri: Uri; Status: Status; Data: IGraph }
type ResumeRequest = { StepNumber: int; StepUri: Uri; Data: IGraph }

type Workflow (configuration: IGraph, steps: Steps, workflow: IGraph) =
    let validate() =
        let asm = Reflection.Assembly.GetExecutingAssembly()
        let dir = IO.FileInfo(asm.Location).Directory.FullName
        let parse file = IO.Path.Combine(dir, file) |> Utils.parseTurtleFile
        let shapes, schema = parse "shapes.ttl", parse "schema.ttl"
        infer [schema] workflow
        let shapesGraph = new ShapesGraph(shapes)
        let report = shacl shapesGraph workflow
        if not report.Conforms
        then failwithf "Invalid Workflow: %A" report.Results
        
    do
        workflow.Merge configuration
        validate()

    let final state =
        let result = (Schema.FinalStep state.Step).Success.Single
        { state with StepNumber = state.StepNumber+1; Result = Some result }

    let next state stepType data =
        let nextStep = (steps.Get stepType) data
        { state with StepNumber = state.StepNumber+1; Step = nextStep }

    let getStepType state =
        state.Step.Node.Types
        |> Seq.filter (fun t -> t <> Classes.Step)
        |> Seq.exactlyOne

    let run (data: IGraph, state: State) =

        let rec runSteps state =
            let stepType = getStepType state
            if stepType = Classes.YieldStep then state
            elif stepType = Classes.FinalStep then final state
            else runSteps (next state state.Step data)

        runSteps state, data

    let response (state, data) =
        let status =
             match state.Result with
             | None -> Suspended
             | Some true -> Succeded
             | Some false -> Failed
        { StepNumber = state.StepNumber; StepUri = state.Step.Node.Uri; Status = status; Data = data }

    member _.Start(input: IGraph) =
        input.Merge configuration
        let w = Schema.Workflow.Get(workflow).Single
        let state = { StepNumber = 0; Step = w.StartAt.Single.Resource; Result = None }
        run (input, state)
        |> response

    member _.Resume(request: ResumeRequest) =
        let yieldNode = request.StepUri |> workflow.GetUriNode 
        let yieldStep = Schema.YieldStep { Graph = workflow; Node = yieldNode }
        let state = {StepNumber = request.StepNumber+1; Step = yieldStep.Next.Single.Resource; Result = None}
        run (request.Data, state)
        |> response

