module Workflow

#if INTERACTIVE
#r "nuget: Iride"
#endif

open VDS.RDF
open VDS.RDF.Query
open Iride
open Utils

type Schema = GraphProvider<Schema = "schema.ttl">
type Classes = UriProvider<"schema.ttl", SchemaQuery.RdfsClasses>
    
type State = { Id: int; Step: Schema.Step; Result: bool option } 

let run 
    (configuration: IGraph) 
    (sparqlFactory: string -> SparqlQuery)
    (workflow: IGraph)
    (input: IGraph) =
    
    let information = new Graph()
    information.Merge configuration
    information.Merge input

    let askStep node =
        let step = Schema.AskStep node
        if ask (sparqlFactory step.Sparql.Single) information
        then step.NextOnTrue.Single
        else step.NextOnFalse.Single

    let constructStep node =
        let step = Schema.ConstructStep node
        information
        |> construct (sparqlFactory step.Sparql.Single) 
        |> information.Merge
        step.Next.Single

    let updateStep node = 
        let step = Schema.DatabaseUpdateStep node
        //TODO
        step.Next.Single

    let finalStep node = (Schema.FinalStep node).Success.Single

    let nextState state nextStep = { state with Id = state.Id+1; Step = nextStep }

    let finalState state result = { state with Id = state.Id+1; Result = Some result }
    
    let next step state = step state.Step.Node |> nextState state

    let final state = finalStep state.Step.Node |> finalState state

    // TODO: external call, suspend call, subprocess\procedure call
    let steps = dict [
        Classes.AskStep, next askStep
        Classes.ConstructStep, next constructStep
        Classes.DatabaseUpdateStep, next updateStep
        Classes.FinalStep, final ]

    let rec runSteps state =  
        match state.Result with
        | Some result -> result
        | None -> 
            let stepType = state.Step.Node.Types.Single
            match steps.TryGetValue stepType with
            | true, step -> runSteps (step state)
            | _ -> failwith $"Unknown step {stepType}"
        
    let w = Schema.Process.Get(workflow).Single
    let success = runSteps { Id = 0; Step = w.StartAt.Single; Result = None }
    success, information
