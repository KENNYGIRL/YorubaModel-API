using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using Spectrogram;
using YorubaModelML.Model;

namespace YorubaPredictionAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(ILogger<WeatherForecastController> logger, IWebHostEnvironment env)
        {
            _env = env;
            _logger = logger;
        }

        [HttpGet]
        public IEnumerable<WeatherForecast> Get()
        {
            var rng = new Random();
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = rng.Next(-20, 55),
                Summary = Summaries[rng.Next(Summaries.Length)]
            })
            .ToArray();
        }

       
        

        [HttpPost("GetPrediction")]
        public async Task<ActionResult> GetPredictionAsync([FromForm] string page, [FromForm] IFormFile audioFile)
        {
            try
            {
                var modelPath = ""; 

                if (page == "Login")
                {
                    modelPath = Path.Combine(_env.ContentRootPath, "Models", "MLModelLogin.zip");
                }
                else if (page == "Language")
                {
                    modelPath = Path.Combine(_env.ContentRootPath, "Models", "MLModelLanguage.zip");
                }
                else if (page == "Home")
                {
                    modelPath = Path.Combine(_env.ContentRootPath, "Models", "MLModelHome.zip");
                }
                else if (page == "cart")
                {
                    modelPath = Path.Combine(_env.ContentRootPath, "Models", "MLModelCartPage.zip");
                }
                else if (page == "singleItem")
                {
                    modelPath = Path.Combine(_env.ContentRootPath, "Models", "MLModelSingleItemPage.zip");
                }
                else if (page == "category")
                {
                    modelPath = Path.Combine(_env.ContentRootPath, "Models", "MLModelCategory.zip");
                }
                else if (page == "pay")
                {
                    //var v = @"C:\Users\Frank\source\repos\YorubaModelML\YorubaPredictionAPI\Models\MLModelPayPage.zip";
                    modelPath = Path.Combine(_env.ContentRootPath, "Models", "MLModelPayPage.zip");
                }



                var model = new ConsumeModel(modelPath);

                var uploadPath = Path.Combine(_env.ContentRootPath, "uploads");
                Directory.CreateDirectory(uploadPath);

                if (audioFile.Length > 0)
                {
                    var audioFilePath = Path.Combine(uploadPath, audioFile.FileName);

                    using (var fs = new FileStream(audioFilePath, FileMode.Create))
                    {
                        await audioFile.CopyToAsync(fs);
                    }

                    double[] audio;
                    int sampleRate;
                    using (var audioFileReader = new AudioFileReader(audioFilePath))
                    {
                        sampleRate = audioFileReader.WaveFormat.SampleRate;
                        var wholeFile = new List<float>((int)(audioFileReader.Length / 4));
                        var readBuffer = new float[audioFileReader.WaveFormat.SampleRate * audioFileReader.WaveFormat.Channels];
                        int samplesRead = 0;
                        while ((samplesRead = audioFileReader.Read(readBuffer, 0, readBuffer.Length)) > 0)
                            wholeFile.AddRange(readBuffer.Take(samplesRead));
                        audio = Array.ConvertAll(wholeFile.ToArray(), x => (double)x);
                    }

                    int fftSize = 8192;
                    var spec = new Spectrogram.Spectrogram(sampleRate, 4096, stepSize: 500, maxFreq: 3000,fixedWidth: 250);
                    spec.Add(audio);
                    var info = new FileInfo(audioFilePath);
                    var imagepath = Path.Combine(uploadPath ,  info.Name + ".png");
                    spec.SaveImage(imagepath, intensity: 20_000);


                    var md = new ModelInput { ImageSource = imagepath };
                    var result = model.Predict(md);
                    Directory.Delete(uploadPath, true);
                    return Ok(new { class_id = result.Prediction, probability = result.Score.Max() });
                    //return Ok(_env.ContentRootPath);

                }
                _logger.LogError("File is Null");
                return BadRequest("File is null");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest(ex.Message);
            }
            

        }
    }
}
