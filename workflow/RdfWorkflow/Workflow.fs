namespace RdfWorkflow

open System
open VDS.RDF
open VDS.RDF.Query
open Iride
open Utils

type Schema = GraphProvider<Schema = "schema.ttl">
type Classes = UriProvider<"schema.ttl", SchemaQuery.RdfsClasses>
    
type State = { StepNumber: int; Step: INode; Result: bool option } 

type Status = Succeded | Failed | Suspended

type Response = { StepNumber: int; StepUri: Uri; Status: Status; Data: IGraph }
type ResumeRequest = { StepNumber: int; StepUri: Uri; Data: IGraph }

type Workflow
    (configuration: IGraph,
    sparqlFactory: string -> SparqlQuery,
    workflow: IGraph) =

    let finalStep node = (Schema.FinalStep node).Success.Single

    let nextState state nextStep = 
        { state with StepNumber = state.StepNumber+1; Step = nextStep }

    let finalState state result = 
        { state with StepNumber = state.StepNumber+1; Result = Some result }
    
    let next step state = step state.Step |> nextState state

    let final state = finalStep state.Step |> finalState state

    let run (input: IGraph, state: State) =
    
        let information = new Graph()
        information.Merge configuration
        information.Merge input

        let askStep node =
            let step = Schema.AskStep node
            if ask (sparqlFactory step.Sparql.Single) information
            then step.NextOnTrue.Single.Node
            else step.NextOnFalse.Single.Node

        let constructStep node =
            let step = Schema.ConstructStep node
            information
            |> construct (sparqlFactory step.Sparql.Single) 
            |> information.Merge
            step.Next.Single.Node

        let updateStep node = 
            let step = Schema.DatabaseUpdateStep node
            //TODO
            step.Next.Single.Node

        // TODO: custom call, subprocess\procedure call
        let steps = dict [
            Classes.AskStep, askStep
            Classes.ConstructStep, constructStep
            Classes.DatabaseUpdateStep, updateStep ]

        let rec runSteps state =  
            let stepType = state.Step.Types.Single
            if stepType = Classes.YieldStep then state
            elif stepType = Classes.FinalStep then final state
            else 
                match steps.TryGetValue stepType with
                | true, step -> runSteps (next step state)
                | _ -> failwith $"Unknown step {stepType}"
      
        runSteps state, information

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
        let yieldStep = 
            Schema.YieldStep.Get(workflow) 
            |> Seq.filter (fun x -> x.Node.Uri = request.StepUri)
            |> Seq.exactlyOne
        let state = {StepNumber = request.StepNumber+1; Step = yieldStep.Next.Single.Node; Result = None}
        run (request.Data, state)
        |> response
