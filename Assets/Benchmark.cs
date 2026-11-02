using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

public class BenchmarkManager : MonoBehaviour
{
    [System.Serializable]
    public struct ParticleTestConfig
    {
        public string styleName;
        public GameObject particlePrefab;
        public int emissionRate;
    }

    public List<ParticleTestConfig> testSuite;
    public float warmupTime = 2.0f;     // Tiempo para que el sistema se estabilice
    public float testDuration = 5.0f;    // Tiempo de medición efectiva
    
    private List<string> csvLines = new List<string>();
    private string filePath;

    void Start()
    {
        filePath = Path.Combine(Application.persistentDataPath, "Particle_Benchmark_Results.csv");
        csvLines.Add("StyleName,EmissionRate,FrameCount,FPS,FrameTime_ms,CPU_MainThread_ms,RenderThread_ms,GPUTime_ms");
        StartCoroutine(RunBenchmarkRoutine());
    }

    IEnumerator RunBenchmarkRoutine()
    {
        foreach (var test in testSuite)
        {
            GameObject currentInstance = Instantiate(test.particlePrefab, Vector3.zero, Quaternion.identity);
            
            // Opcional: Ajustar el Rate over Time si el prefab expone el ParticleSystem
            ParticleSystem ps = currentInstance.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                var emission = ps.emission;
                emission.rateOverTime = test.emissionRate;
            }

            // 1. Fase de Warm-up (descartar picos iniciales de instanciación)
            yield return new WaitForSeconds(warmupTime);

            // 2. Fase de Medición
            float elapsedTime = 0f;
            int framesAcc = 0;
            float totalFrameTime = 0f;
            float totalCpuTime = 0f;
            float totalRenderThreadTime = 0f;
            float totalGpuTime = 0f;

            Recorder mainThreadRecorder = Recorder.Get("Main Thread");
            Recorder renderThreadRecorder = Recorder.Get("Render Thread");

            while (elapsedTime < testDuration)
            {
                float frameTime = Time.unscaledDeltaTime;
                totalFrameTime += frameTime;

                // Tiempos en milisegundos extraídos mediante Profiling Recorders
                totalCpuTime += mainThreadRecorder.elapsedNanoseconds / 1000000f;
                totalRenderThreadTime += renderThreadRecorder.elapsedNanoseconds / 1000000f;
                
                // Nota: El tiempo de GPU requiere FrameTimingManager si se ejecuta en una Build sin Profiler
                totalGpuTime += FrameTimingManager.GetGpuTimerFrequency() > 0 ? (float)FrameTimingManager.GetCpuTimerFrequency() : 0f;

                framesAcc++;
                elapsedTime += frameTime;
                yield return null;
            }

            // Cálculo de promedios para la prueba actual
            float avgFps = framesAcc / totalFrameTime;
            float avgFrameTimeMs = (totalFrameTime / framesAcc) * 1000f;
            float avgCpuMs = totalCpuTime / framesAcc;
            float avgRenderThreadMs = totalRenderThreadTime / framesAcc;
            float avgGpuMs = totalGpuTime / framesAcc;

            string line = $"{test.styleName},{test.emissionRate},{framesAcc},{avgFps:F2},{avgFrameTimeMs:F2},{avgCpuMs:F2},{avgRenderThreadMs:F2},{avgGpuMs:F2}";
            csvLines.Add(line);

            Destroy(currentInstance);
            yield return new WaitForSeconds(1.0f); // Pausa breve entre pruebas
        }

        // 3. Exportar a CSV
        File.WriteAllLines(filePath, csvLines.ToArray());
        Debug.Log($"Pruebas finalizadas. Archivo CSV guardado en: {filePath}");
    }
}