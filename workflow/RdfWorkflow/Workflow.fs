namespace RdfWorkflow

open System
open VDS.RDF
open VDS.RDF.Query
open Iride
open Utils

type Schema = GraphProvider<Schema = "schema.ttl">
type Classes = UriProvider<"schema.ttl", SchemaQuery.RdfsClasses>

  
type Steps(sparqlFactory: string -> SparqlQuery) =

    let askStep data node =
        let step = Schema.AskStep node
        if ask (sparqlFactory step.Sparql.Single) data
        then step.NextOnTrue.Single.Node
        else step.NextOnFalse.Single.Node

    let constructStep data node =
        let step = Schema.ConstructStep node
        data
        |> construct (sparqlFactory step.Sparql.Single) 
        |> data.Merge
        step.Next.Single.Node

    let updateStep data node = 
        let step = Schema.DatabaseUpdateStep node
        //TODO
        step.Next.Single.Node

    let steps = dict [
        Classes.AskStep, askStep
        Classes.ConstructStep, constructStep
        Classes.DatabaseUpdateStep, updateStep ]

    member _.Get(stepTypeUri) =
        match steps.TryGetValue stepTypeUri with
        | true, step -> step
        | _ -> failwith $"Unknown step {stepTypeUri}"

type State = { StepNumber: int; Step: INode; Result: bool option } 

type Status = Succeded | Failed | Suspended

type Response = { StepNumber: int; StepUri: Uri; Status: Status; Data: IGraph }
type ResumeRequest = { StepNumber: int; StepUri: Uri; Data: IGraph }

type Workflow
    (configuration: IGraph,
    steps: Steps,
    workflow: IGraph) =

    let finalStep node = (Schema.FinalStep node).Success.Single

    let nextState state nextStep = 
        { state with StepNumber = state.StepNumber+1; Step = nextStep }

    let finalState state result = 
        { state with StepNumber = state.StepNumber+1; Result = Some result }
    
    let next step state = step state.Step |> nextState state

    let final state = finalStep state.Step |> finalState state

    let run (input: IGraph, state: State) =
        let data = new Graph()
        data.Merge configuration
        data.Merge input

        let rec runSteps state =  
            let stepType = state.Step.Types.Single
            if stepType = Classes.YieldStep then state
            elif stepType = Classes.FinalStep then final state
            else 
                let step = steps.Get(stepType) data
                runSteps (next step state) 

        runSteps state, data

    let response (state, data) =
        let status = 
             match state.Result with
             | None -> Suspended
             | Some true -> Succeded
             | Some false -> Failed
        { StepNumber = state.StepNumber; StepUri = state.Step.Uri; Status = status; Data = data }

    member _.Start(input: IGraph) =
        let p = Schema.Process.Get(workflow).Single
        let state = { StepNumber = 0; Step = p.StartAt.Single.Node; Result = None }
        run (input, state)
        |> response

    member _.Resume(request: ResumeRequest) =
        let yieldStep = request.StepUri |> workflow.GetUriNode |> Schema.YieldStep
        let state = {StepNumber = request.StepNumber+1; Step = yieldStep.Next.Single.Node; Result = None}
        run (request.Data, state)
        |> response
