Shader "Custom/RayTracer"
{
	Properties{
		_MainTexOld("Albedo (RGB)", 2D) = "black" {}
	}
	SubShader{
		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			// Input structure for vertex shader
			struct appdata {
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			// Output structure for vertex shader, used to pass data to fragment shader
			struct v2f {
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			// Vertex shader function
			v2f vert(appdata v) {
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			float3 ViewParams;
			float4x4 CamLocalToWorldMatrix;
			uniform sampler2D _MainTexOld;

			bool EnvironmentLighting;

			float4 SkyColorHorizon;
			float4 SkyColorZenith;
			float4 GroundColor;

			float3 SunLightDirection;
			float SunFocus;
			float SunIntensity;

			int MaxBounceCount;
			int NumRaysPerPixel;
			int NumRenderedFrames;
			int Frame;

			struct Ray {
				float3 origin;
				float3 dir;
			};

			struct RayTracingMaterial {
				float4 color;
				float4 emissionColor;
				float emissionStrength;
			};

			struct HitInfo {
				bool didHit;
				float dist;
				float3 hitPoint;
				float3 normal;
				RayTracingMaterial material;
			};

			struct Sphere {
				float3 position;
				float radius;
				RayTracingMaterial material;
			};

			struct Triangle {
				float3 a;
				float3 b;
				float3 c;
				float3 norm;
				RayTracingMaterial material;
			};

			StructuredBuffer<Sphere> spheres;
			int numSpheres;

			StructuredBuffer<Triangle> triangles;
			int numTriangles;

			// Calculate the intersection of a ray with a sphere
			HitInfo RaySphere(Ray ray, float3 sphereCentre, float sphereRadius) {
				HitInfo hitInfo = (HitInfo)0;
				float3 offsetRayOrigin = ray.origin - sphereCentre;
				// sqrtLength(rayOrigin + rayDir * dist) = radius^2
				// Solving for dst creates quadratic equation with coefficients:
				float a = dot(ray.dir, ray.dir); // a = 1 (assume unit vector)
				float b = 2 * dot(offsetRayOrigin, ray.dir);
				float c = dot(offsetRayOrigin, offsetRayOrigin) - sphereRadius * sphereRadius;
				// Quadratic discriminant
				float discriminant = b * b - 4 * a * c;

				// No solution for negative discriminant (ray misses sphere)
				if (discriminant >= 0) {
					// Distance to nearest intersection (from quadratic formula)
					float dist = (-b - sqrt(discriminant)) / (2 * a);

					// Ignore intersections behind the ray
					if (dist >= 0) {
						hitInfo.didHit = true;
						hitInfo.dist = dist;
						hitInfo.hitPoint = ray.origin + ray.dir * dist;
						hitInfo.normal = normalize(hitInfo.hitPoint - sphereCentre);
					}
				}
				return hitInfo;
			}

			HitInfo RayTriangle(Ray ray, Triangle tri) {
				float3 AB = tri.b - tri.a;
				float3 AC = tri.c - tri.a;
				float3 normal = tri.norm;

				float determinant = -dot(ray.dir, normal);
				float invDet = 1.0 / determinant;

				float3 ao = ray.origin - tri.a; // Vector from point on triangle to origin
				float3 dao = cross(ao, ray.dir); // Approaches 0 as ray gets close to ao

				// Calculate dist to triangle & barycentric coordinates of interesection point
				float dist = dot(ao, normal) * invDet; // t
				float u = dot(AC, dao) * invDet;
				float v = -dot(AB, dao) * invDet;
				float w = 1 - u - v;

				// Initialize HitInfo
				HitInfo hitInfo = (HitInfo)0;
				hitInfo.didHit = determinant >= 1E-6 && dist >= 0 && u >= 0 && v >= 0 && (u + v) <= 1.0;
				hitInfo.hitPoint = ray.origin + ray.dir * dist;
				hitInfo.normal = normal;
				hitInfo.dist = dist;
				return hitInfo;
			}

			float RandomValue(inout uint state) {
				state = state * 747796405 + 2891336453;
				uint result = ((state >> ((state >> 28) + 4)) ^ state) * 277803737;
				result = (result >> 22) ^ result;
				return result / 4294967295.0;
			}

			float RandomNormalValue(inout uint state) {
				float theta = 2 * 3.1415926 * RandomValue(state);
				float rho = sqrt(-2 * log(RandomValue(state)));
				return rho * cos(theta);
			}

			float3 RandomDirection(inout uint state) {
				for (int safetyLimit = 0; safetyLimit < 100; safetyLimit++) {
					float x = RandomNormalValue(state) * 2 - 1;
					float y = RandomNormalValue(state) * 2 - 1;
					float z = RandomNormalValue(state) * 2 - 1;
					float3 pointInCube = float3(x, y, z);
					float sqrtDistFromCentre = dot(pointInCube, pointInCube);
					if (sqrtDistFromCentre <= 1) {
						return pointInCube / sqrt(sqrtDistFromCentre);
					}
				}
				return 0;
			}

			// Find first point that the given ray collides with and return hit info
			HitInfo CalculateRayCollision(Ray ray) {
				HitInfo closestHit = (HitInfo)0;
				closestHit.dist = 1.#INF;

				for (int i = 0; i < numSpheres; i++) {
					Sphere sphere = spheres[i];
					HitInfo hitInfo = RaySphere(ray, sphere.position, sphere.radius);

					if (hitInfo.didHit && hitInfo.dist < closestHit.dist) {
						closestHit = hitInfo;
						closestHit.material = sphere.material;
					}
				}

				for (int i = 0; i < numTriangles; i++) {
					Triangle tri = triangles[i];
					HitInfo hitInfo = RayTriangle(ray, tri);

					if (hitInfo.didHit && hitInfo.dist < closestHit.dist) {
						closestHit = hitInfo;
						closestHit.material = tri.material;
					}
				}
				
				return closestHit;
			}

			float4 GetEnvironmentLight(Ray ray) {
				float skyGradientT = pow(smoothstep(0, 0.4, ray.dir.y), 0.35);
				float4 skyGradient = lerp(SkyColorHorizon, SkyColorZenith, skyGradientT);
				float sun = pow(max(0, dot(ray.dir, -SunLightDirection)), SunFocus) * SunIntensity;

				float groundToSkyT = smoothstep(-0.01, 0, ray.dir.y);
				float sunMask = groundToSkyT >= 1;
				return lerp(GroundColor, skyGradient, groundToSkyT) + sun * sunMask;
			}

			float4 Trace(Ray ray, inout uint state) {
				float4 incomingLight = 0;
				float4 rayColor = 1;
				for (int i = 0; i <= MaxBounceCount; i++) {
					HitInfo hitInfo = CalculateRayCollision(ray);
					if (hitInfo.didHit) {
						ray.origin = hitInfo.hitPoint;
						ray.dir = normalize(hitInfo.normal + RandomDirection(state));

						RayTracingMaterial material = hitInfo.material;
						float4 emittedLight = material.emissionColor * material.emissionStrength;
						incomingLight += emittedLight * rayColor;
						rayColor *= material.color;
					}
					else {
						if (EnvironmentLighting) {
							incomingLight += GetEnvironmentLight(ray) * rayColor;
						}
						break;
					}
				}
				return incomingLight;
			}

			float4 frag(v2f i) : SV_Target{
				// Seed for randomness
				uint2 numPixels = _ScreenParams.xy;
				uint2 pixelCoord = i.uv * numPixels;
				uint pixelIndex = pixelCoord.y * numPixels.x + pixelCoord.x;
				uint rngState = pixelIndex + Frame * 719393;

				// create ray
				float3 viewPointLocal = float3(i.uv - 0.5, 1) * ViewParams;
				float3 viewPoint = mul(CamLocalToWorldMatrix, float4(viewPointLocal, 1));

				Ray ray;
				ray.origin = _WorldSpaceCameraPos;
				ray.dir = normalize(viewPoint - ray.origin);

				// Pixel color
				float4 totalIncomingLight = 0;
				for (int rayIndex = 0; rayIndex < NumRaysPerPixel; rayIndex++) {
					totalIncomingLight += Trace(ray, rngState);
				}
				// Average traces
				float4 pixelColor = totalIncomingLight / NumRaysPerPixel;

				// Temporal average
				float4 oldPixel = tex2D(_MainTexOld, i.uv);
				float weight = 1.0 / (NumRenderedFrames + 1);
				float4 accumulatedAverage = oldPixel * (1 - weight) + pixelColor * weight;

				return accumulatedAverage;
			}

			ENDCG
		}
	}
		FallBack "Diffuse"
}
