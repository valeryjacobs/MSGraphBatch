# MS Graph Batching

When using MS Graph to manage inboxes and calandars you might end up needing to submit multiple request in one go. MS Graph has batching support for this but I might work a little different than you expect. 

Basically you can optimize the overhead of submitting multiple reperate request by turning them into a single batch payload but internally MS Graph still handles each request reflected in the payload seperately. This means you have to walk through the response to see whether each request succeeded as this is not an atomic transaction. Below is a description of how a batch can be constructed and handled at runtime.

In this sample we use the MS Graph SDK package Microsoft.Graph but all of this 

## Getting Started

We setup a batch of requests by creating a BatchRequestContent object and inserting a series of BatchRequestStep objects which contain the HttpRequestMessage object that holds a request object that is exactly the same as the ones you would submit seperately.

```
 List<BatchRequestContent> batches = new List<BatchRequestContent>();
 
 var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, $"https://graph.microsoft.com/v1.0/users/{config["CalendarEmail"]}/events")
                {
                    Content = new StringContent(JsonConvert.SerializeObject(e), Encoding.UTF8, "application/json")
                };

BatchRequestStep requestStep = new BatchRequestStep(events.IndexOf(e).ToString(), httpRequestMessage, null);
batchRequestContent.AddBatchRequestStep(requestStep);
 ```
Next we submit the batch to MS Graph which under the hood calls out to the https://graph.microsoft.com/v1.0/$batch endpoint with a payload that combines all requests in this format:

```
"requests": [
    {
      "id": "1",
      "method": "GET",
      "url": "/me/drive/root:/{file}:/content"
    },
    ...
```
The ID is important as we need to track the status of these request and because the response is unordered.

After the post we call GetResponsesAsync() to get a dictionary to walk through. We can call each individual response by using the Id (dictionary key).


```
response = await client.Batch.Request().PostAsync(batch);
Dictionary<string, HttpResponseMessage> responses = await response.GetResponsesAsync();

   foreach (string key in responses.Keys)
                {
                    HttpResponseMessage httpResponse = await response.GetResponseByIdAsync(key);
                    var responseContent = await httpResponse.Content.ReadAsStringAsync();

                   ...
```

The response clearly show us the result come in randomly.

![alt text](https://github.com/valeryjacobs/MSGraphBatch/edit/master/images/unorderedresponse.png "Unordered response")


### Considerations

What things you need to install the software and how to install them

```
Give examples
```

### Installing

A step by step series of examples that tell you how to get a development env running

Say what the step will be

```
Give the example
```

And repeat

```
until finished
```

End with an example of getting some data out of the system or using it for a little demo

## Running the tests

Explain how to run the automated tests for this system

### Break down into end to end tests

Explain what these tests test and why

```
Give an example
```

### And coding style tests

Explain what these tests test and why

```
Give an example
```

## Deployment

Add additional notes about how to deploy this on a live system

## Built With

* [Dropwizard](http://www.dropwizard.io/1.0.2/docs/) - The web framework used
* [Maven](https://maven.apache.org/) - Dependency Management
* [ROME](https://rometools.github.io/rome/) - Used to generate RSS Feeds

## Contributing

Please read [CONTRIBUTING.md](https://gist.github.com/PurpleBooth/b24679402957c63ec426) for details on our code of conduct, and the process for submitting pull requests to us.

## Versioning

We use [SemVer](http://semver.org/) for versioning. For the versions available, see the [tags on this repository](https://github.com/your/project/tags). 

## Authors

* **Billie Thompson** - *Initial work* - [PurpleBooth](https://github.com/PurpleBooth)

See also the list of [contributors](https://github.com/your/project/contributors) who participated in this project.

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details

## Acknowledgments

* Hat tip to anyone whose code was used
* Inspiration
* etc
