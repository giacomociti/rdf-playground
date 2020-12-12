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
    let workflowGraph = Utils.parseTurtle "workflow1.ttl"
    let queryFactory _ = SparqlQueryParser().ParseFromString s1
    let w = Workflow(cfg, queryFactory, workflowGraph)
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
    let workflowGraph = Utils.parseTurtle "workflow1.ttl"
    let queryFactory _ = SparqlQueryParser().ParseFromString s1
    let w = Workflow(cfg, queryFactory, workflowGraph)
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
let ``Yield and resume`` () =
    let cfg = new Graph()
    let workflowGraph = Utils.parseTurtle "workflow2.ttl"
    let queryFactory _ = failwith "no need"
    let w = Workflow(cfg, queryFactory, workflowGraph)
    
    let response = w.Start(new Graph())
    Assert.Equal(Status.Suspended, response.Status)
    Assert.Equal(0, response.StepNumber)
    Assert.Equal(Uri "http://example.org/s1", response.StepUri)
    
    let finalResponse = w.Resume { StepNumber = response.StepNumber; StepUri = response.StepUri; Data = new Graph() }
    Assert.Equal(Status.Succeded, finalResponse.Status)
    Assert.Equal(2, finalResponse.StepNumber)
    Assert.Equal(Uri "http://example.org/ok", finalResponse.StepUri)