using System;
using System.Diagnostics;


namespace VRABowling
{
    class newSide
    {
        float[] times, coords, detections;
        float speed, x0, curX, x, s, speed_corr=0, gridStep, err;
        int sideNum, crosses, prev_ray, ray_step, trustRN;
        bool enable_XFilt;
        Stopwatch watch;

#if DEBUG
        public System.IO.StreamWriter obtf, gcf;
#endif

        public newSide(int detNum, float gridStep, float ballRadius, int sideNum)
        {
            times = new float[detNum];
            coords = new float[detNum + 1];
            for (int i = 0; i < detNum; i++)
                coords[i] = i * gridStep + ballRadius;
            coords[detNum] = 9999999;
            detections = coords;
            watch = new Stopwatch();
            this.sideNum = sideNum;
            this.gridStep = gridStep;
            prev_ray = 0;
            ray_step = 1;

            enable_XFilt = true;
            trustRN = 4;
        }

        public void OnBallTracked(int ray, float time)
        {
            curX = GetCoord(0);
            err = Math.Abs(curX - coords[ray]) / this.gridStep;

            if (err < 0.2 || crosses < trustRN)
            {
                x0 = curX;
                if (enable_XFilt && crosses > (trustRN - 1))
                {
                    detections[ray] = curX * err + coords[ray] * (1 - err);
                }
                watch.Reset();
                watch.Start();

                times[ray] = time;
                crosses++;

                float k = 0;

                if (crosses < 3)
                    x0 = coords[ray];

                if (crosses < 2)
                {
                    speed = 0;
                    s = 50;
                }
                else
                {
                    var speed0 = speed;
                    float dt = time - times[prev_ray];
                    speed = (detections[ray] - detections[prev_ray]) / dt;

                    if (crosses > 2)
                    {
                        var dv = Math.Abs(speed - speed0);
                        s += dv * dv;
                        var s1 = (float)Math.Sqrt(s / crosses);
                        k = dv * dv / (dv * dv + (dv + s1) * s1 * 0.5f);
                        speed = speed0 * k + speed * (1 - k);
                    }

                    speed_corr = (detections[ray] - x0) * 2 / dt;


                }
                ray_step = ray - prev_ray;
                prev_ray = ray;
#if DEBUG
            obtf.WriteLine(
                "{0,12}|{1,12}|{2,12}|{3,12}|{4,12}|{5,12}|{6,12}|{7,12}",
                sideNum, ray, time, speed, speed_corr, coords[ray] - x0, k, s);
#endif
            }
        }

        public float GetCoord(float delay)
        {
            float dt = 0;
            dt = watch.ElapsedMilliseconds / 1000.0f + delay;

            if (prev_ray > 0  &&  dt > (times[prev_ray] - times[prev_ray - ray_step]) / 2)
            {
                speed_corr = 0;
                x0 = detections[prev_ray];
            }

            x = x0 + (speed + speed_corr) * dt;
            if (prev_ray < 2 && delay > 0)
            {
                x = -1000;
            }
            return x;
        }
    }
}
