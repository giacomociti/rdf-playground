module Tests

open Xunit
open VDS.RDF
open VDS.RDF.Parsing
open RdfWorkflow
open System

[<Fact>]
let ``Failure`` () =
    let cfg = new Graph()
    let s1 = "ASK WHERE { ?input a <http://example.org/test> }"
    let workflowGraph = Utils.parseTurtleFile "workflow1.ttl"
    let f = dict ["s1.rq", s1] |> Utils.factory
    let w = Workflow(cfg, Steps([], f), workflowGraph)
    let input = new Graph()

    let response = w.Start(input)
    Assert.Equal(Status.Failed, response.Status)
    Assert.Equal(2, response.StepNumber)
    Assert.Equal(Uri "http://example.org/ko", response.StepUri)
    Assert.True(response.Data.IsEmpty)

[<Fact>]
let ``Success`` () =
    let cfg = new Graph()
    let s1 = "ASK WHERE { ?input a <http://example.org/test> }"
    let workflowGraph = Utils.parseTurtleFile "workflow1.ttl"
    let f = dict ["s1.rq", s1] |> Utils.factory
    let w = Workflow(cfg, Steps([], f), workflowGraph)
    let input = new Graph()
    let subject = input.CreateBlankNode()
    let predicate = input.CreateUriNode(UriFactory.Create RdfSpecsHelper.RdfType)
    let object = input.CreateUriNode(UriFactory.Create "http://example.org/test")
    input.Assert(subject, predicate, object)

    let response = w.Start(input)
    Assert.Equal(Status.Succeded, response.Status)
    Assert.Equal(2, response.StepNumber)
    Assert.Equal(Uri "http://example.org/ok", response.StepUri)
    Assert.Equal(1, response.Data.Triples.Count)

[<Fact>]
let ``Shacl`` () =
    let workflow = Utils.parseTurtleText  """
    @prefix w: <http://workflow.org/> .
    @prefix : <http://example.org/> .
    
    :w1 a w:Workflow ;
        w:startAt :s1 .
    :s1 a w:ShaclStep ;
        w:shapes "s1.ttl" ;
        w:nextOnValid  :ok ;
        w:nextOnInvalid :ko .
    :ko a w:FinalStep ;
        w:success false .
    :ok a w:FinalStep ;
        w:success true .
    """
    let s1 = """
    @prefix rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#> .
    @prefix sh: <http://www.w3.org/ns/shacl#> .
    @prefix ex:	<http://example.org/> . 

    ex:PersonCountShape a sh:NodeShape ;
        sh:targetNode ex:Person ;
        sh:property [
            sh:path [ sh:inversePath rdf:type ] ;
            sh:minCount 1 ;
        ] .
    """
    let f = dict ["s1.ttl", s1] |> Utils.factory
    let w = Workflow(new Graph(), Steps([], f), workflow)
    let validInput = new Graph()

    let subject = validInput.CreateBlankNode()
    let predicate = validInput.CreateUriNode(UriFactory.Create RdfSpecsHelper.RdfType)
    let object = validInput.CreateUriNode(UriFactory.Create "http://example.org/Person")
    validInput.Assert(subject, predicate, object)
    
    let response = w.Start(validInput)
    Assert.Equal(Status.Succeded, response.Status)
    Assert.Equal(2, response.StepNumber)
    Assert.Equal(Uri "http://example.org/ok", response.StepUri)
    Assert.Equal(3, response.Data.Triples.Count)

[<Fact>]
let ``Yield and resume`` () =
    let cfg = new Graph()
    let workflowGraph = Utils.parseTurtleFile "workflow2.ttl"
    let f = dict [] |> Utils.factory

    let w = Workflow(cfg, Steps([], f), workflowGraph)
    
    let response = w.Start(new Graph())
    Assert.Equal(Status.Suspended, response.Status)
    Assert.Equal(0, response.StepNumber)
    Assert.Equal(Uri "http://example.org/s1", response.StepUri)
    
    let finalResponse = w.Resume { StepNumber = response.StepNumber; StepUri = response.StepUri; Data = new Graph() }
    Assert.Equal(Status.Succeded, finalResponse.Status)
    Assert.Equal(2, finalResponse.StepNumber)
    Assert.Equal(Uri "http://example.org/ok", finalResponse.StepUri)

[<Fact>]
let ``Inference`` () =
    let cfg = new Graph()
    let workflowGraph = Utils.parseTurtleText """
    @prefix w: <http://workflow.org/> .
    @prefix : <http://example.org/> .
    
    :w2 a w:Workflow ;
        w:startAt :s1 .
    :s1 a w:RdfsInferenceStep ;
        w:next :ok .
    :ok a w:FinalStep ;
        w:success true .
    """

    let f = dict [] |> Utils.factory
    
    let w = Workflow(cfg, Steps([], f), workflowGraph)
        
    let input = new Graph()
    let subject = input.CreateBlankNode()
    let predicate = input.CreateUriNode(UriFactory.Create RdfSpecsHelper.RdfType)
    let object = input.CreateUriNode(UriFactory.Create "http://example.org/Person")
    input.Assert(subject, predicate, object)
    
    let response = w.Start(input)
    Assert.Equal(Status.Succeded, response.Status)
    Assert.Equal(2, response.StepNumber)
    Assert.Equal(Uri "http://example.org/ok", response.StepUri)
        
    
[<Fact>]
let ``Custom step`` () =
    let cfg = new Graph()
    let workflowGraph = Utils.parseTurtleFile "workflow3.ttl"
    let f = dict [] |> Utils.factory
    let mutable called = false
    let myStep _ = called <- true
    let myStepUri = Uri "http://example.org/MyStep"
    let customSteps = [myStepUri, myStep]
    let w = Workflow(cfg, Steps(customSteps, f), workflowGraph)
    
    let response = w.Start(new Graph())
    Assert.Equal(Status.Succeded, response.Status)
    Assert.Equal(2, response.StepNumber)
    Assert.Equal(Uri "http://example.org/ok", response.StepUri)
    Assert.True(called)
