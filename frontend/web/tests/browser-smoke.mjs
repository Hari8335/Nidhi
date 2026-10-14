import { readFile, readdir, mkdir } from 'node:fs/promises';
import { randomBytes } from 'node:crypto';
import assert from 'node:assert/strict';
// Run only against a disposable local database and private email pickup directory.
// The optional browser driver is installed outside the project; see README.
if (!process.env.NIDHI_BROWSER_CONFIG) throw new Error('Set NIDHI_BROWSER_CONFIG to a private JSON configuration file.');
const config = JSON.parse(await readFile(process.env.NIDHI_BROWSER_CONFIG, 'utf8'));
const { chromium } = await import(config.playwrightModule);
const origin = config.baseUrl || 'http://localhost:3000';
assert.ok(['localhost', '127.0.0.1'].includes(new URL(origin).hostname), 'This smoke test is restricted to localhost.');
const output = config.outputDirectory;
await mkdir(output,{recursive:true});
const admin = config;
const email=`browser-${randomBytes(6).toString('hex')}@example.test`;
const password=`Aa1!${randomBytes(16).toString('hex')}`;
const nextPassword=`Bb2!${randomBytes(16).toString('hex')}`;
const browser=await chromium.launch({executablePath: config.executablePath,headless:true});
const context=await browser.newContext({viewport:{width:1440,height:1000}});
const page=await context.newPage();
let stage='initialization';
const errors=[];page.on('pageerror',()=>errors.push('pageerror'));
const seen=[];page.on('response',r=>{ if(r.url().includes('/api/v1/auth/'))seen.push({path:new URL(r.url()).pathname,status:r.status()}); });
async function goto(path){await page.goto(origin+path);await page.locator('h1').waitFor();}
async function screenshot(label){await page.screenshot({path:`${output}/${label}.png`,fullPage:true,animations:'disabled'});}
async function checkOverflow(){assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'horizontal overflow');}
async function fill(name,value){await page.getByLabel(name,{exact:true}).fill(value);}
async function click(name){await page.getByRole('button',{name,exact:true}).click();}
async function waitText(text){await page.getByText(text,{exact:false}).first().waitFor();}
async function pickup(purpose){
 for(let i=0;i<30;i++){
  for(const f of await readdir(config.emailPickupDirectory).catch(()=>[])){
   const message=JSON.parse(await readFile(config.emailPickupDirectory+'/'+f,'utf8'));
   if(message.email===email && message.purpose===purpose)return message;
  }
  await new Promise(r=>setTimeout(r,100));
 }
 throw new Error('pickup missing');
}
try {
 stage='anonymous visual review';
 for(const width of [1440,390,768]){
  await page.setViewportSize({width,height:1000});
  for(const route of ['/','/register','/login','/verify-email','/forgot-password','/reset-password']){
   await goto(route);await checkOverflow();
   if(width!==768)await screenshot(`${route==='/'?'home':route.slice(1)}-${width}`);
  }
 }
 console.log('PASS: all public/auth pages at 390, 768, 1440 pixels; no overflow');
 await page.setViewportSize({width:1440,height:1000});
 stage='registration';await goto('/register');
 await fill('Display name (optional)','Session Six');await fill('Email address',email);await fill('Password',password);await click('Create account');
 await waitText('Your account is ready.');
 assert.ok(seen.some(r=>r.path.endsWith('/antiforgery')&&r.status===200));
 assert.ok(seen.some(r=>r.path.endsWith('/register')&&r.status===201));
 console.log('PASS: browser registration and automatic antiforgery');
 stage='verification';const verify=await pickup('verification');
 await page.getByRole('link',{name:'Continue to verification'}).click();
 await fill('Account ID',verify.userId);await fill('Verification code',verify.token);await click('Verify email');await waitText('Email verified.');
 console.log('PASS: private development pickup and browser verification');
 stage='customer login';await goto('/login');await fill('Email address',email);await fill('Password',password);await click('Sign in');await page.waitForURL('**/dashboard');await waitText('Welcome, Session Six');
 assert.ok(seen.some(r=>r.path.endsWith('/me')&&r.status===200));
 const cookies=await context.cookies();assert.ok(cookies.find(c=>c.name==='.Nidhi.Session'&&c.httpOnly&&c.sameSite==='Strict'));
 await page.reload();await waitText('Welcome, Session Six');
 for(const width of [1440,390,768]){await page.setViewportSize({width,height:1000});await checkOverflow();if(width!==768)await screenshot(`dashboard-${width}`);}
 await goto('/profile');for(const width of [1440,390]){await page.setViewportSize({width,height:1000});await checkOverflow();await screenshot(`profile-${width}`);}
 await page.getByRole('button',{name:'More',exact:true}).click();await page.getByRole('navigation',{name:'More destinations'}).waitFor();await click('Close menu');
 stage='customer denied admin';await page.goto(origin+'/admin');await page.waitForURL('**/dashboard');await waitText('Welcome, Session Six');
 console.log('PASS: cookie persistence, safe profile, customer shell, mobile More menu, customer denied admin');
 stage='logout';await click('Sign out');await page.waitForURL('**/login');await page.goto(origin+'/dashboard');await page.waitForURL('**/login');
 console.log('PASS: real logout and protected-route redirect');
 stage='forgot password';await goto('/forgot-password');await fill('Email address',email);await click('Send reset instructions');await waitText('If an account exists for this email');
 const reset=await pickup('password-reset');
 stage='reset password';await page.getByRole('link',{name:'I have a reset code'}).click();await fill('Account ID',reset.userId);await fill('Reset code',reset.token);await fill('New password',nextPassword);await fill('Confirm new password',nextPassword);await click('Reset password');await waitText('Your password has been reset.');
 await page.getByRole('link',{name:'Continue to sign in'}).click();await fill('Email address',email);await fill('Password',nextPassword);await click('Sign in');await page.waitForURL('**/dashboard');await waitText('Welcome, Session Six');await click('Sign out');await page.waitForURL('**/login');
 console.log('PASS: browser recovery, reset, and new-password login');
 stage='admin login';await fill('Email address',admin.adminEmail);await fill('Password',admin.adminPassword);await click('Sign in');await page.waitForURL('**/admin');await waitText('A clear view, as we grow.');
 for(const width of [1440,390,768]){await page.setViewportSize({width,height:1000});await checkOverflow();if(width!==768)await screenshot(`admin-${width}`);}
 await page.setViewportSize({width:390,height:1000});await page.getByText('Administration menu',{exact:true}).click();await page.getByRole('navigation',{name:'Mobile administration'}).waitFor();
 await page.goto(origin+'/dashboard');await page.waitForURL('**/admin');await click('Sign out');await page.waitForURL('**/login');
 console.log('PASS: provisioned ADMIN login, shell, responsive navigation, ADMIN excluded from customer shell');
 stage='validation and accessibility';await goto('/register');await fill('Email address',email);await fill('Password','lowercaseonlypassword');await click('Create account');await page.locator('.alert-error').waitFor();assert.equal(await page.getByLabel('Password',{exact:true}).getAttribute('aria-invalid'),'true');await screenshot('register-error-mobile');
 await goto('/login');await page.keyboard.press('Tab');await screenshot('keyboard-focus-mobile');
 await page.emulateMedia({reducedMotion:'reduce'});await goto('/register');assert.equal(await page.locator('.auth-card').evaluate(el=>getComputedStyle(el).animationName),'none');await screenshot('reduced-motion-mobile');
 assert.deepEqual(await page.evaluate(()=>({local:localStorage.length,session:sessionStorage.length})),{local:0,session:0});assert.equal(errors.length,0);
 console.log('PASS: inline validation, keyboard focus, reduced motion, empty browser storage, no uncaught JS errors');
 stage='invalid token and duplicate email';
 await goto('/verify-email');await fill('Account ID','00000000-0000-0000-0000-000000000001');await fill('Verification code','invalid-test-code');await click('Verify email');await waitText('invalid, expired, or already used');
 await goto('/register');await fill('Email address',email);await fill('Password',nextPassword);await click('Create account');await waitText('This email is already registered.');
 stage='network, rate-limit and loading UX';
 // Supplemental UI fault injection; the end-to-end flow above uses the real API.
 await goto('/login');await fill('Email address',email);await fill('Password',nextPassword);
 await page.route('**/api/v1/auth/login',route=>route.fulfill({status:429,headers:{'Content-Type':'application/problem+json','Retry-After':'30'},body:JSON.stringify({code:'RATE_LIMITED'})}));
 await click('Sign in');await waitText('Try again in 30 seconds.');await page.unroute('**/api/v1/auth/login');
 await page.route('**/api/v1/auth/login',route=>route.abort());await click('Sign in');await waitText('We could not connect.');await page.unroute('**/api/v1/auth/login');
 await goto('/dashboard');await page.waitForURL('**/login');
 console.log('PASS: real invalid-token/duplicate-email errors and supplemental rate-limit/network UI states');
 console.log('ALL BROWSER CHECKS PASSED');
} catch(error){console.error(`FAILED stage: ${stage}; ${error.name}. Sensitive details suppressed.`);process.exitCode=1;}
finally{await browser.close();}
