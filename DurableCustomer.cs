using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace Company.Function
{
    public static class DurableCustomer
    {
        [FunctionName("DurableCustomer")]
        public static async Task<int> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            Customer customer =  context.GetInput<Customer>();


            int customerId = await context.CallActivityAsync<int>("DurableCustomer_AddToSQL", customer);


            var retryOptions = new RetryOptions(System.TimeSpan.FromSeconds(5), 3)
            {
                Handle = ex => ex.Message.Contains("Network Error")
            };

            int returnId = await context.CallActivityWithRetryAsync<int>("DurableCustomer_AddCRM",retryOptions,customerId);

            return returnId;
        }

        [FunctionName("DurableCustomer_AddToSQL")]
        public static int AddToSQL([ActivityTrigger] Customer customer, ILogger log)
        {
            //insert to SQL logic
            int customerId = customer.id; //should be populated with the reusult
            return customerId;
        }

        [FunctionName("DurableCustomer_AddCRM")]
        public static int AddToCRM([ActivityTrigger] int customerId, ILogger log)
        {
            //insert to CRM
            int returnId = customerId * 10; //return code from CRM insert
            throw new System.Exception("Network Error");
            //return returnId;
        }

        [FunctionName("DurableCustomer_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.

            var customer = await req.Content.ReadAsAsync<Customer>();

            string instanceId = await starter.StartNewAsync("DurableCustomer", customer);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}