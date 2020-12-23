namespace RdfWorkflow

open System
open Iride
open Utils
open VDS.RDF
open VDS.RDF.Update

type Schema = GraphProvider<Schema = "schema.ttl">
type Classes = UriProvider<"schema.ttl", SchemaQuery.RdfsClasses>

type Steps (customSteps, factory: IFactory) =

    let askStep node data =
        let step = Schema.AskStep node
        if ask (factory.CreateQuery step.SparqlQuery.Single) data
        then step.NextOnTrue.Single.Node
        else step.NextOnFalse.Single.Node

    let shaclStep node data =
        let step = Schema.ShaclStep node
        let result = shacl (factory.CreateShaclShape step.Shapes.Single) data
        data.Merge result.Graph
        if result.Conforms 
        then step.NextOnValid.Single.Node
        else step.NextOnInvalid.Single.Node

    let constructStep node data =
        let step = Schema.ConstructStep node
        data
        |> construct (factory.CreateQuery step.SparqlQuery.Single) 
        |> data.Merge
        step.Next.Single.Node

    let rdfsInferenceStep node data =
        let step = Schema.RdfsInferenceStep node
        infer data
        step.Next.Single.Node


    let remoteUpdateStep node (data: IGraph) = 
        let step = Schema.RemoteUpdateStep node
        let endpointUri = step.RemoteEndpoint.Single.Endpoint.Single.Uri
        let endpoint = SparqlRemoteUpdateEndpoint(endpointUri)
        let argsQuery = factory.CreateQuery step.ArgumentsQuery.Single
        let update = factory.CreateUpdate step.SparqlUpdate.Single
        let args = select argsQuery data
        let command = getCommands update args |> String.concat ";\n"
        endpoint.Update command
        step.Next.Single.Node

    let coreSteps = [
        Classes.AskStep, askStep
        Classes.ConstructStep, constructStep
        Classes.ShaclStep, shaclStep
        Classes.RdfsInferenceStep, rdfsInferenceStep
        Classes.RemoteUpdateStep, remoteUpdateStep ]

    let customStep action node data = 
        action data
        (Schema.Step node).Next.Single.Node

    let additionalSteps = 
        customSteps
        |> List.map (fun (stepUri, action) -> stepUri, customStep action )

    let steps = dict (coreSteps @ additionalSteps)

    member _.Get(stepTypeUri) =
        match steps.TryGetValue stepTypeUri with
        | true, step -> step
        | _ -> failwith $"Unknown step {stepTypeUri}"

type State = { StepNumber: int; Step: INode; Result: bool option } 

type Status = Succeded | Failed | Suspended

type Response = { StepNumber: int; StepUri: Uri; Status: Status; Data: IGraph }
type ResumeRequest = { StepNumber: int; StepUri: Uri; Data: IGraph }

type Workflow (configuration: IGraph, steps: Steps, workflow: IGraph) =
    
    do 
        workflow.Merge configuration

    let final state = 
        let result = (Schema.FinalStep state.Step).Success.Single 
        { state with StepNumber = state.StepNumber+1; Result = Some result }

    let next state stepType data =
        let nextStep = (steps.Get stepType) state.Step data
        { state with StepNumber = state.StepNumber+1; Step = nextStep }

    let run (data: IGraph, state: State) =

        let rec runSteps state =  
            let stepType = state.Step.Types.Single
            if stepType = Classes.YieldStep then state
            elif stepType = Classes.FinalStep then final state
            else runSteps (next state stepType data)
                
        runSteps state, data

    let response (state, data) =
        let status = 
             match state.Result with
             | None -> Suspended
             | Some true -> Succeded
             | Some false -> Failed
        { StepNumber = state.StepNumber; StepUri = state.Step.Uri; Status = status; Data = data }

    member _.Start(input: IGraph) =
        input.Merge configuration

        let p = Schema.Process.Get(workflow).Single
        let state = { StepNumber = 0; Step = p.StartAt.Single.Node; Result = None }
        run (input, state)
        |> response

    member _.Resume(request: ResumeRequest) =
        let yieldStep = request.StepUri |> workflow.GetUriNode |> Schema.YieldStep
        let state = {StepNumber = request.StepNumber+1; Step = yieldStep.Next.Single.Node; Result = None}
        run (request.Data, state)
        |> response
